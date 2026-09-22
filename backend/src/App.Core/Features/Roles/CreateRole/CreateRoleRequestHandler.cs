using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Roles.CreateRole;

/// <summary>
/// 新增角色用例：名称唯一 → 权限点合法性 → 同一事务内插角色 + 全量插权限点集合
/// </summary>
public sealed class CreateRoleRequestHandler : IRequestHandler<CreateRoleRequest, RoleDetailDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化新增角色用例处理器
    /// </summary>
    public CreateRoleRequestHandler(
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理新增角色请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<RoleDetailDto> HandleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        // 查库约束：角色名称大小写不敏感唯一（specs/028-erp-rbac/design.md §3.4）
        if (await _roleRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.RoleNameExists, "角色名称已存在");
        }

        // 业务约束：权限点必须是后端已登记的常量（编辑角色同理）
        var permissionKeys = PermissionKeys.NormalizeOrThrow(request.PermissionKeys);
        var remark = NullIfWhiteSpace(request.Remark);

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Remark = remark,
            IsBuiltin = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 角色与其权限点集合必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _roleRepository.AddAsync(role, cancellationToken);
            await _roleRepository.ReplacePermissionsAsync(role.Id, permissionKeys, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return RoleDtoMapper.ToRoleDetailDto(role, permissionKeys, 0);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
