namespace App.Core.Features.Users;

/// <summary>
/// 用户入参归一化辅助：可选字段的空白串统一按"未填写"（null）处理，避免唯一索引出现空串冲突。
/// </summary>
internal static class UserInputNormalizer
{
    /// <summary>
    /// 去除首尾空白；空串 / 全空白返回 null
    /// </summary>
    /// <param name="value">原始文本</param>
    public static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 邮箱归一化：去首尾空白后转小写（邮箱唯一性忽略大小写，统一小写存储避免同一邮箱以不同大小写重复落库）
    /// </summary>
    /// <param name="value">原始邮箱文本</param>
    public static string? NormalizeEmail(string? value)
        => NullIfWhiteSpace(value)?.ToLowerInvariant();
}
