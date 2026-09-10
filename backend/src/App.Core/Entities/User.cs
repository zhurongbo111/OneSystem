namespace App.Core.Entities;

/// <summary>
/// 用户实体（对应 PostgreSQL 表 Users）。
/// 用户由管理员在后台创建，无注册入口；密码只存哈希，登录时经 PasswordHasher 校验。
/// </summary>
public sealed class User
{
    /// <summary>用户 ID</summary>
    public Guid Id { get; set; }

    /// <summary>登录名，唯一，创建后不可修改</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码哈希（PBKDF2 编码串），禁止存储明文</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>邮箱，可空；非空时唯一</summary>
    public string? Email { get; set; }

    /// <summary>手机号，可空；非空时唯一</summary>
    public string? Phone { get; set; }

    /// <summary>用户状态（启用 / 禁用）</summary>
    public UserStatus Status { get; set; } = UserStatus.Enabled;

    /// <summary>最近登录时间；列表展示用冗余字段，登录明细见 <see cref="UserLoginLog"/></summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id（系统种子创建时为空）</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
