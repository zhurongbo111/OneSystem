namespace App.Core.Errors;

/// <summary>
/// 全局错误码定义，与 AGENTS.md 第 4.2 节一致；业务错误码扩展在各功能 design.md 中定义
/// </summary>
public static class ErrorCode
{
    /// <summary>成功</summary>
    public const int Success = 0;

    /// <summary>参数错误</summary>
    public const int Validation = 40000;

    /// <summary>未登录或 token 无效</summary>
    public const int Unauthorized = 40100;

    /// <summary>无权限</summary>
    public const int Forbidden = 40300;

    /// <summary>资源不存在</summary>
    public const int NotFound = 40400;

    /// <summary>服务内部错误</summary>
    public const int Internal = 50000;

    /// <summary>用户名或密码错误（project-scaffold 功能业务码）</summary>
    public const int LoginFailed = 40001;

    /// <summary>用户名已存在（user-management 功能业务码）</summary>
    public const int UsernameExists = 40002;

    /// <summary>邮箱已被使用</summary>
    public const int EmailExists = 40003;

    /// <summary>手机号已被使用</summary>
    public const int PhoneExists = 40004;

    /// <summary>账号已被禁用（登录时）</summary>
    public const int UserDisabled = 40005;

    /// <summary>不能禁用当前登录账号</summary>
    public const int CannotDisableSelf = 40006;
}
