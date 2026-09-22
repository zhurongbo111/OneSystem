using App.Core.Abstractions;
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

    /// <summary>
    /// 初始化删除角色用例处理器
    /// </summary>
    public DeleteRoleRequestHandler(
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _roleRepository.DeleteAsync(role, cancellationToken);
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
