using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Departments.CreateDepartment;

/// <summary>
/// 新增部门用例：编码唯一 → 上级存在性 → 同一上级下名称唯一 → 落库（与审计日志同事务）
/// </summary>
public sealed class CreateDepartmentRequestHandler : IRequestHandler<CreateDepartmentRequest, DepartmentDetailDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增部门用例处理器
    /// </summary>
    public CreateDepartmentRequestHandler(
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
    /// 处理新增部门请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<DepartmentDetailDto> HandleAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（大小写不敏感）
        if (await _departmentRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.DepartmentCodeExists, "部门编码已存在");
        }

        // 查库约束：上级部门必须存在（新建部门无后代，无需防环）
        if (request.ParentId is not null
            && await _departmentRepository.GetByIdAsync(request.ParentId.Value, cancellationToken) is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "上级部门不存在");
        }

        // 查库约束：同一上级下名称唯一（不同上级允许同名）
        if (await _departmentRepository.ExistsByNameAsync(name, request.ParentId, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.DepartmentNameExists, "同一上级下部门名称已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var department = new Department
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            Status = (DepartmentStatus)request.Status,
            Remark = NullIfWhiteSpace(request.Remark),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _departmentRepository.AddAsync(department, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "部门编码", null, department.Code)
                .Add("name", "部门名称", null, department.Name)
                .Add("parentId", "上级部门", null, department.ParentId?.ToString())
                .Add("sortOrder", "排序", null, AuditSummary.Count(department.SortOrder))
                .Add("status", "状态", null, AuditText.DepartmentStatus(department.Status))
                .Add("remark", "备注", null, department.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Department,
                Action = AuditAction.Create,
                ResourceId = department.Id,
                ResourceNo = department.Code,
                Summary = $"新增部门 {department.Name}（{department.Code}）",
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

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
