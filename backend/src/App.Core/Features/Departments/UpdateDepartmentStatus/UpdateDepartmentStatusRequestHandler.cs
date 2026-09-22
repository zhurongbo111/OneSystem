using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Departments.UpdateDepartmentStatus;

/// <summary>
/// 部门停用 / 启用用例：存在性 → 更新状态（与审计日志同事务）。
/// 停用后不参与新增下级与员工选择，历史数据保留。
/// </summary>
public sealed class UpdateDepartmentStatusRequestHandler : IRequestHandler<UpdateDepartmentStatusRequest, DepartmentDetailDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化部门停用 / 启用用例处理器
    /// </summary>
    public UpdateDepartmentStatusRequestHandler(
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
    /// 处理部门停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<DepartmentDetailDto> HandleAsync(UpdateDepartmentStatusRequest request, CancellationToken cancellationToken = default)
    {
        var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "部门不存在");

        var beforeStatus = department.Status;
        var now = DateTimeOffset.UtcNow;
        department.Status = (DepartmentStatus)request.Status;
        department.UpdatedAt = now;
        department.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _departmentRepository.UpdateAsync(department, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.DepartmentStatus(beforeStatus), AuditText.DepartmentStatus(department.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Department,
                Action = AuditAction.StatusChange,
                ResourceId = department.Id,
                ResourceNo = department.Code,
                Summary = $"{(department.Status == DepartmentStatus.Enabled ? "启用" : "停用")}部门 {department.Name}（{department.Code}）",
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
}
