using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Api.Authorization;

/// <summary>
/// 动作级权限点标注：标注的动作要求当前登录用户拥有指定权限点（多个标注为「且」关系）。
/// 由 <see cref="PermissionAuthorizationFilter"/> 在授权阶段读取并执行校验，
/// 校验失败统一返回 HTTP 200 + <c>code = 40300</c>（见 AGENTS.md §4.2）。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IFilterMetadata
{
    /// <summary>要求的权限点 key（取值见 App.Core.Auth.Permissions）</summary>
    public string PermissionKey { get; }

    /// <summary>
    /// 初始化权限点标注
    /// </summary>
    /// <param name="permissionKey">权限点 key</param>
    public RequirePermissionAttribute(string permissionKey)
    {
        PermissionKey = permissionKey;
    }
}
