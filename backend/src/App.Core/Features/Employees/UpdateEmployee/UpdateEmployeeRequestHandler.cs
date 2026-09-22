using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Employees.UpdateEmployee;

/// <summary>
/// 编辑员工用例：存在性 → 手机 / 邮箱 / 账号唯一性（排除自身）→ 部门 / 岗位 / 账号存在性
/// → 状态与离职日期一致性 → 更新（与审计日志同事务）。
/// 工号不在请求体内（创建后不可修改）。
/// </summary>
public sealed class UpdateEmployeeRequestHandler : IRequestHandler<UpdateEmployeeRequest, EmployeeDetailDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑员工用例处理器
    /// </summary>
    public UpdateEmployeeRequestHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IPositionRepository positionRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _positionRepository = positionRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑员工请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<EmployeeDetailDto> HandleAsync(UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        var name = request.Name.Trim();
        var phone = EmployeeInputNormalizer.NullIfWhiteSpace(request.Phone);
        var email = EmployeeInputNormalizer.NormalizeEmail(request.Email);

        // 查库约束：手机 / 邮箱仅对非空值判定唯一（均排除自身）
        if (phone is not null && await _employeeRepository.ExistsByPhoneAsync(phone, employee.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.EmployeePhoneExists, "手机号已存在");
        }

        if (email is not null && await _employeeRepository.ExistsByEmailAsync(email, employee.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.EmployeeEmailExists, "邮箱已存在");
        }

        // 查库约束：部门 / 岗位非空时必须存在
        if (request.DepartmentId is not null
            && await _departmentRepository.GetByIdAsync(request.DepartmentId.Value, cancellationToken) is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "部门不存在");
        }

        if (request.PositionId is not null
            && await _positionRepository.GetByIdAsync(request.PositionId.Value, cancellationToken) is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "岗位不存在");
        }

        // 查库约束：账号必须存在，且未被其他员工绑定（排除自身）
        if (request.UserId is not null)
        {
            if (await _userRepository.GetByIdAsync(request.UserId.Value, cancellationToken) is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "账号不存在");
            }

            if (await _employeeRepository.ExistsByUserIdAsync(request.UserId.Value, employee.Id, cancellationToken))
            {
                throw new BusinessException(ErrorCode.EmployeeUserBound, "该账号已绑定其他员工");
            }
        }

        var beforeName = employee.Name;
        var beforeGender = employee.Gender;
        var beforePhone = employee.Phone;
        var beforeEmail = employee.Email;
        var beforeDepartmentId = employee.DepartmentId;
        var beforePositionId = employee.PositionId;
        var beforeHireDate = employee.HireDate;
        var beforeResignDate = employee.ResignDate;
        var beforeStatus = employee.Status;
        var beforeUserId = employee.UserId;
        var beforeRemark = employee.Remark;

        var now = DateTimeOffset.UtcNow;
        var status = (EmployeeStatus)request.Status;
        employee.Name = name;
        employee.Gender = request.Gender is null ? null : (Gender)request.Gender.Value;
        employee.Phone = phone;
        employee.Email = email;
        employee.DepartmentId = request.DepartmentId;
        employee.PositionId = request.PositionId;
        employee.HireDate = request.HireDate;
        employee.ResignDate = EmployeeInputNormalizer.ResolveResignDate(status, request.ResignDate, now);
        employee.Status = status;
        employee.UserId = request.UserId;
        employee.Remark = EmployeeInputNormalizer.NullIfWhiteSpace(request.Remark);
        employee.UpdatedAt = now;
        employee.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _employeeRepository.UpdateAsync(employee, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("name", "姓名", beforeName, employee.Name)
                .Add("gender", "性别", AuditText.Gender(beforeGender), AuditText.Gender(employee.Gender))
                .Add("phone", "手机号", beforePhone, employee.Phone)
                .Add("email", "邮箱", beforeEmail, employee.Email)
                .Add("departmentId", "部门", beforeDepartmentId?.ToString(), employee.DepartmentId?.ToString())
                .Add("positionId", "岗位", beforePositionId?.ToString(), employee.PositionId?.ToString())
                .Add("hireDate", "入职日期", AuditSummary.Date(beforeHireDate), AuditSummary.Date(employee.HireDate))
                .Add("resignDate", "离职日期", AuditSummary.Date(beforeResignDate), AuditSummary.Date(employee.ResignDate))
                .Add("status", "状态", AuditText.EmployeeStatus(beforeStatus), AuditText.EmployeeStatus(employee.Status))
                .Add("userId", "关联账号", beforeUserId?.ToString(), employee.UserId?.ToString())
                .Add("remark", "备注", beforeRemark, employee.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Employee,
                Action = AuditAction.Update,
                ResourceId = employee.Id,
                ResourceNo = employee.EmployeeNo,
                Summary = $"修改员工 {employee.Name}（{employee.EmployeeNo}）",
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

        return await EmployeeDetailLoader.LoadAsync(_employeeRepository, employee.Id, cancellationToken);
    }
}
