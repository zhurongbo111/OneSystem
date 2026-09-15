namespace App.Core.Abstractions;

/// <summary>
/// <see cref="ICurrentUser"/> 扩展：统一解析当前登录用户 id，各用例 / 仓储一律经本方法获取，禁止各自内联解析。
/// </summary>
public static class ICurrentUserExtensions
{
    /// <summary>
    /// 解析当前登录用户 id；claims 缺失或非法时返回 null（审计字段允许为空）
    /// </summary>
    /// <param name="currentUser">当前登录用户抽象</param>
    public static Guid? UserId(this ICurrentUser currentUser)
        => Guid.TryParse(currentUser.Id, out var id) ? id : null;
}
