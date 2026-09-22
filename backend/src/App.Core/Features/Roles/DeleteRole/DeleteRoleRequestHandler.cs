using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Roles.DeleteRole;

/// <summary>
/// 删除角色用例：存在性 → 内置限制 → 用户占用检查 → 删除角色及其权限行（同一事务）
/// </summary>
public sealed class DeleteRoleRequestHandler : IRequestHandler<DeleteRoleRequest, object?>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除角色用例处理器
    /// </summary>
    public DeleteRoleRequestHandler(
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除角色请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "角色不存在");

        if (role.IsBuiltin)
        {
            throw new BusinessException(ErrorCode.RoleBuiltinImmutable, "内置角色不可删除");
        }

        // 查库约束：角色已被用户绑定时禁止删除（避免出现"无角色用户"）
        if (await _userRoleRepository.CountByRoleAsync(role.Id, cancellationToken) > 0)
        {
            throw new BusinessException(ErrorCode.RoleInUse, "角色已被用户绑定，禁止删除");
        }

        // 删除前取出权限点集合：角色行删除后其权限行随之消失，日志必须留住"删掉了什么权限"
        var beforePermissionKeys = await _roleRepository.GetPermissionKeysAsync(role.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _roleRepository.DeleteAsync(role, cancellationToken);

            var deletedRoleChangeBuilder = new AuditChangeBuilder()
                .Add("name", "角色名称", role.Name, null)
                .Add("permissionKeys", "权限点", AuditSummary.Join(beforePermissionKeys.Select(App.Core.Auth.Permissions.LabelOf)), null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Role,
                Action = AuditAction.Delete,
                ResourceId = role.Id,
                ResourceNo = role.Name,
                Summary = $"删除角色 {role.Name}",
                Changes = deletedRoleChangeBuilder.Build(),
                ChangesTruncated = deletedRoleChangeBuilder.Truncated,
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
