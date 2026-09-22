using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Departments.DeleteDepartment;

/// <summary>
/// 删除部门用例：存在性 → 无子部门且无在职员工（删除保护）→ 物理删除（与审计日志同事务）。
/// 组织历史由停用承载，删除仅对「空叶子」开放。
/// </summary>
public sealed class DeleteDepartmentRequestHandler : IRequestHandler<DeleteDepartmentRequest, object?>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除部门用例处理器
    /// </summary>
    public DeleteDepartmentRequestHandler(
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除部门请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "部门不存在");

        // 查库约束：有子部门禁止删除（避免产生孤儿子树）
        if (await _departmentRepository.HasChildrenAsync(department.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.DepartmentInUse, "该部门存在子部门，不可删除");
        }

        // 查库约束：有员工引用禁止删除（含离职员工：员工表对部门建的是 Restrict 外键）
        var employeeCount = await _departmentRepository.CountEmployeesAsync(department.Id, cancellationToken);
        if (employeeCount > 0)
        {
            throw new BusinessException(ErrorCode.DepartmentInUse, $"该部门存在 {employeeCount} 名员工，不可删除");
        }

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _departmentRepository.DeleteAsync(department, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "部门编码", department.Code, null)
                .Add("name", "部门名称", department.Name, null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Department,
                Action = AuditAction.Delete,
                ResourceId = department.Id,
                ResourceNo = department.Code,
                Summary = $"删除部门 {department.Name}（{department.Code}）",
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

        return null;
    }
}
