using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Employees.CreateEmployee;

/// <summary>
/// 新增员工用例：工号 / 手机 / 邮箱 / 账号唯一性 → 部门 / 岗位 / 账号存在性 → 落库（与审计日志同事务）
/// </summary>
public sealed class CreateEmployeeRequestHandler : IRequestHandler<CreateEmployeeRequest, EmployeeDetailDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增员工用例处理器
    /// </summary>
    public CreateEmployeeRequestHandler(
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
    /// 处理新增员工请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<EmployeeDetailDto> HandleAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var employeeNo = request.EmployeeNo.Trim();
        var name = request.Name.Trim();
        var phone = EmployeeInputNormalizer.NullIfWhiteSpace(request.Phone);
        var email = EmployeeInputNormalizer.NormalizeEmail(request.Email);

        // 查库约束：工号全局唯一（大小写不敏感）
        if (await _employeeRepository.ExistsByNoAsync(employeeNo, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.EmployeeNoExists, "工号已存在");
        }

        // 查库约束：手机 / 邮箱仅对非空值判定唯一
        if (phone is not null && await _employeeRepository.ExistsByPhoneAsync(phone, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.EmployeePhoneExists, "手机号已存在");
        }

        if (email is not null && await _employeeRepository.ExistsByEmailAsync(email, null, cancellationToken))
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

        // 查库约束：账号必须存在，且未被其他员工绑定（一个账号最多绑一个员工）
        if (request.UserId is not null)
        {
            if (await _userRepository.GetByIdAsync(request.UserId.Value, cancellationToken) is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "账号不存在");
            }

            if (await _employeeRepository.ExistsByUserIdAsync(request.UserId.Value, null, cancellationToken))
            {
                throw new BusinessException(ErrorCode.EmployeeUserBound, "该账号已绑定其他员工");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var status = (EmployeeStatus)request.Status;
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = employeeNo,
            Name = name,
            Gender = request.Gender is null ? null : (Gender)request.Gender.Value,
            Phone = phone,
            Email = email,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            HireDate = request.HireDate,
            ResignDate = EmployeeInputNormalizer.ResolveResignDate(status, request.ResignDate, now),
            Status = status,
            UserId = request.UserId,
            Remark = EmployeeInputNormalizer.NullIfWhiteSpace(request.Remark),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _employeeRepository.AddAsync(employee, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("employeeNo", "工号", null, employee.EmployeeNo)
                .Add("name", "姓名", null, employee.Name)
                .Add("gender", "性别", null, AuditText.Gender(employee.Gender))
                .Add("phone", "手机号", null, employee.Phone)
                .Add("email", "邮箱", null, employee.Email)
                .Add("departmentId", "部门", null, employee.DepartmentId?.ToString())
                .Add("positionId", "岗位", null, employee.PositionId?.ToString())
                .Add("hireDate", "入职日期", null, AuditSummary.Date(employee.HireDate))
                .Add("resignDate", "离职日期", null, AuditSummary.Date(employee.ResignDate))
                .Add("status", "状态", null, AuditText.EmployeeStatus(employee.Status))
                .Add("userId", "关联账号", null, employee.UserId?.ToString())
                .Add("remark", "备注", null, employee.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Employee,
                Action = AuditAction.Create,
                ResourceId = employee.Id,
                ResourceNo = employee.EmployeeNo,
                Summary = $"新增员工 {employee.Name}（{employee.EmployeeNo}）",
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
