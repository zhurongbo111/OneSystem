using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Roles.UpdateRole;

/// <summary>
/// 编辑角色用例：存在性 → 内置限制（SuperAdmin 不可编辑）→ 名称唯一（排除自身）→ 权限点合法性
/// → 同一事务内全量替换权限点 + 更新审计字段
/// </summary>
public sealed class UpdateRoleRequestHandler : IRequestHandler<UpdateRoleRequest, RoleDetailDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑角色用例处理器
    /// </summary>
    public UpdateRoleRequestHandler(
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑角色请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<RoleDetailDto> HandleAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "角色不存在");

        // 查库约束以外的业务约束：内置超级管理员不可编辑（不可改名 / 改备注 / 改权限）
        if (role.IsBuiltin && string.Equals(role.Name, BuiltinRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCode.RoleBuiltinImmutable, "内置超级管理员角色不可修改");
        }

        var name = request.Name.Trim();
        if (await _roleRepository.ExistsByNameAsync(name, role.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.RoleNameExists, "角色名称已存在");
        }

        var permissionKeys = PermissionKeys.NormalizeOrThrow(request.PermissionKeys);
        var remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        var now = DateTimeOffset.UtcNow;

        // 权限点是全量替换，需先取原集合才能算出增 / 减差异
        var beforePermissionKeys = await _roleRepository.GetPermissionKeysAsync(role.Id, cancellationToken);
        var beforeName = role.Name;
        var beforeRemark = role.Remark;

        role.Name = name;
        role.Remark = remark;
        role.UpdatedAt = now;
        role.UpdatedBy = _currentUser.UserId();

        // 权限点全量替换与角色更新必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _roleRepository.ReplacePermissionsAsync(role.Id, permissionKeys, cancellationToken);
            await _roleRepository.UpdateAsync(role, cancellationToken);

            var permissionDiff = AuditSummary.Diff(beforePermissionKeys, permissionKeys, App.Core.Auth.Permissions.LabelOf);
            var roleChangeBuilder = new AuditChangeBuilder()
                .Add("name", "角色名称", beforeName, role.Name)
                .Add("remark", "备注", beforeRemark, role.Remark)
                .Add("permissionKeys", "权限点", null, permissionDiff);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Role,
                Action = AuditAction.Update,
                ResourceId = role.Id,
                ResourceNo = role.Name,
                Summary = $"编辑角色 {role.Name} 权限变更：{permissionDiff}",
                Changes = roleChangeBuilder.Build(),
                ChangesTruncated = roleChangeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var userCount = await _userRoleRepository.CountByRoleAsync(role.Id, cancellationToken);
        return RoleDtoMapper.ToRoleDetailDto(role, permissionKeys, userCount);
    }
}
