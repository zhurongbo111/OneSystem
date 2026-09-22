using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Departments.UpdateDepartment;

/// <summary>
/// 编辑部门用例：存在性 → 编码唯一 → 上级存在性与防环（不得为自身或自身后代）
/// → 同一上级下名称唯一 → 更新（与审计日志同事务）
/// </summary>
public sealed class UpdateDepartmentRequestHandler : IRequestHandler<UpdateDepartmentRequest, DepartmentDetailDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑部门用例处理器
    /// </summary>
    public UpdateDepartmentRequestHandler(
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑部门请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<DepartmentDetailDto> HandleAsync(UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "部门不存在");

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（排除自身）
        if (await _departmentRepository.ExistsByCodeAsync(code, department.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.DepartmentCodeExists, "部门编码已存在");
        }

        // 查库约束：上级变更时须存在，且不得为自身或自身后代（沿父链上溯，命中即防环失败）
        if (request.ParentId != department.ParentId && request.ParentId is not null)
        {
            if (request.ParentId.Value == department.Id)
            {
                throw new BusinessException(ErrorCode.DepartmentCycle, "上级部门不能是自身或其下级");
            }

            var parentExists = await _departmentRepository.GetByIdAsync(request.ParentId.Value, cancellationToken) is not null;
            if (!parentExists)
            {
                throw new BusinessException(ErrorCode.NotFound, "上级部门不存在");
            }

            if (await IsSelfOrDescendantAsync(department.Id, request.ParentId.Value, cancellationToken))
            {
                throw new BusinessException(ErrorCode.DepartmentCycle, "上级部门不能是自身或其下级");
            }
        }

        // 查库约束：同一上级下名称唯一（排除自身；上级变化后按新上级判定）
        if (await _departmentRepository.ExistsByNameAsync(name, request.ParentId, department.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.DepartmentNameExists, "同一上级下部门名称已存在");
        }

        var beforeCode = department.Code;
        var beforeName = department.Name;
        var beforeParentId = department.ParentId;
        var beforeSortOrder = department.SortOrder;
        var beforeStatus = department.Status;
        var beforeRemark = department.Remark;

        var now = DateTimeOffset.UtcNow;
        department.Code = code;
        department.Name = name;
        department.ParentId = request.ParentId;
        department.SortOrder = request.SortOrder;
        department.Status = (DepartmentStatus)request.Status;
        department.Remark = NullIfWhiteSpace(request.Remark);
        department.UpdatedAt = now;
        department.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _departmentRepository.UpdateAsync(department, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "部门编码", beforeCode, department.Code)
                .Add("name", "部门名称", beforeName, department.Name)
                .Add("parentId", "上级部门", beforeParentId?.ToString(), department.ParentId?.ToString())
                .Add("sortOrder", "排序", AuditSummary.Count(beforeSortOrder), AuditSummary.Count(department.SortOrder))
                .Add("status", "状态", AuditText.DepartmentStatus(beforeStatus), AuditText.DepartmentStatus(department.Status))
                .Add("remark", "备注", beforeRemark, department.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Department,
                Action = AuditAction.Update,
                ResourceId = department.Id,
                ResourceNo = department.Code,
                Summary = $"修改部门 {department.Name}（{department.Code}）",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return DepartmentDtoMapper.ToDepartmentDetailDto(department);
    }

    /// <summary>
    /// 判定候选上级是否位于当前部门子树内（自身或后代）：把全量部门拍平为「子 → 父」映射后沿父链上溯
    /// </summary>
    private async Task<bool> IsSelfOrDescendantAsync(Guid departmentId, Guid candidateParentId, CancellationToken cancellationToken)
    {
        var roots = await _departmentRepository.GetTreeAsync(cancellationToken);
        var parentById = new Dictionary<Guid, Guid?>();
        CollectParentLinks(roots, parentById);

        Guid? cursor = candidateParentId;
        while (cursor is not null)
        {
            if (cursor.Value == departmentId)
            {
                return true;
            }

            cursor = parentById.TryGetValue(cursor.Value, out var parentId) ? parentId : null;
        }

        return false;
    }

    /// <summary>递归拍平树为「部门 id → 上级 id」映射</summary>
    private static void CollectParentLinks(IReadOnlyList<DepartmentTreeNode> nodes, Dictionary<Guid, Guid?> parentById)
    {
        foreach (var node in nodes)
        {
            parentById[node.Id] = node.ParentId;
            CollectParentLinks(node.Children, parentById);
        }
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
