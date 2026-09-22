using App.Core.Errors;

namespace App.Core.Auth;

/// <summary>
/// 权限点集合的归一化与合法性校验，供角色新增 / 编辑用例共用（避免两处规则漂移）。
/// </summary>
internal static class PermissionKeys
{
    /// <summary>
    /// 归一化（Trim + 去重，保持输入顺序）并校验合法性：每一项都必须是 <see cref="Permissions.All"/> 中已登记的 key
    /// </summary>
    /// <param name="keys">原始权限点 key 集合</param>
    /// <returns>归一化后的权限点 key 集合</returns>
    /// <exception cref="BusinessException">存在非法 key 时抛 code 40000</exception>
    public static IReadOnlyList<string> NormalizeOrThrow(IReadOnlyList<string>? keys)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in keys ?? [])
        {
            var key = raw?.Trim() ?? string.Empty;
            if (!Permissions.IsKnown(key))
            {
                throw new BusinessException(ErrorCode.Validation, $"权限点不存在：{key}");
            }

            if (seen.Add(key))
            {
                result.Add(key);
            }
        }

        return result;
    }
}
