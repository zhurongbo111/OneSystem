namespace App.Core.Entities;

/// <summary>
/// 用户相关字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c> 均引用本类常量，
/// 保证"格式校验"与"数据库约束"始终一致，避免同一字段在不同用例中规则分叉。
/// </summary>
public static class UserFieldConstraints
{
    /// <summary>用户名最短长度</summary>
    public const int UsernameMinLength = 3;

    /// <summary>用户名最大长度（对齐 Users.Username varchar(50)）</summary>
    public const int UsernameMaxLength = 50;

    /// <summary>显示名最大长度（对齐 Users.DisplayName varchar(50)）</summary>
    public const int DisplayNameMaxLength = 50;

    /// <summary>邮箱最大长度（对齐 Users.Email varchar(100)）</summary>
    public const int EmailMaxLength = 100;

    /// <summary>手机号最大长度（对齐 Users.Phone varchar(20)）</summary>
    public const int PhoneMaxLength = 20;

    /// <summary>密码最短长度（明文入参，无对应表列；登录 / 创建 / 重置共用）</summary>
    public const int PasswordMinLength = 6;

    /// <summary>密码最大长度（明文入参，无对应表列；登录 / 创建 / 重置共用）</summary>
    public const int PasswordMaxLength = 32;

    /// <summary>客户端 IP 最大长度（对齐 UserLoginLogs.IpAddress varchar(64)）</summary>
    public const int IpAddressMaxLength = 64;

    /// <summary>User-Agent 最大长度（对齐 UserLoginLogs.UserAgent varchar(512)）</summary>
    public const int UserAgentMaxLength = 512;

    /// <summary>手机号格式：中国大陆手机号（固定 11 位，不超过 <see cref="PhoneMaxLength"/>）</summary>
    public const string PhonePattern = @"^1[3-9]\d{9}$";

    /// <summary>用户名格式：<see cref="UsernameMinLength"/>–<see cref="UsernameMaxLength"/> 位字母 / 数字 / 下划线</summary>
    public static string UsernamePattern => $"^[a-zA-Z0-9_]{{{UsernameMinLength},{UsernameMaxLength}}}$";
}
