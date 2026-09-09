namespace App.Core.Entities;

/// <summary>
/// 用户实体。
/// 脚手架阶段无真实数据表，账号由内存仓储种子提供；首个业务功能建表后走 EF Core Migrations，密码改为哈希存储。
/// </summary>
public sealed class User
{
    /// <summary>用户 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>用户名</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>密码（脚手架临时明文，仅用于示例；真实功能必须使用密码哈希与独立凭据校验）</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; init; } = string.Empty;
}
