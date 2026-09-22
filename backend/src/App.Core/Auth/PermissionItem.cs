namespace App.Core.Auth;

/// <summary>
/// 权限点 key：与所属分组一起描述一个可选权限项，供权限树渲染与清单接口使用。
/// key 取值见 <see cref="Permissions"/> 常量。
/// </summary>
/// <param name="Key">权限点 key（&lt;域&gt;.&lt;动作&gt;）</param>
/// <param name="Name">权限点中文名（展示用，前端不硬编码）</param>
public sealed record PermissionItem(string Key, string Name);

/// <summary>
/// 权限点分组（对应一个业务域 / 菜单组），供角色授权界面渲染权限树。
/// </summary>
/// <param name="Name">分组中文名</param>
/// <param name="Items">该分组下的权限点（含 key 与中文名）</param>
public sealed record PermissionGroup(string Name, IReadOnlyList<PermissionItem> Items);
