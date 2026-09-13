using App.Core.Abstractions;

namespace App.Core.Features.Products;

/// <summary>
/// 商品入参辅助：当前登录用户 id 解析（同 user-management 的 UserInputNormalizer.CurrentUserId 语义）。
/// </summary>
internal static class ProductInputNormalizer
{
    /// <summary>
    /// 解析当前登录用户 id；claims 缺失或非法时返回 null（审计字段允许为空）
    /// </summary>
    /// <param name="currentUser">当前登录用户抽象</param>
    public static Guid? CurrentUserId(ICurrentUser currentUser)
        => Guid.TryParse(currentUser.Id, out var id) ? id : null;
}
