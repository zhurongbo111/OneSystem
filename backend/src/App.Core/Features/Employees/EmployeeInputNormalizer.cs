using App.Core.Entities;

namespace App.Core.Features.Employees;

/// <summary>
/// 员工入参归一化辅助：可选字段的空白串统一按"未填写"（null）处理，避免部分唯一索引出现空串冲突
/// （与用户域 <c>UserInputNormalizer</c> 同口径）。
/// </summary>
internal static class EmployeeInputNormalizer
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

    /// <summary>
    /// 离职日期推导：状态为离职且未填日期时补当天（避免出现「离职但无日期」的歧义，specs/030-erp-org-employee/design.md §3.4）
    /// </summary>
    /// <param name="status">在职状态</param>
    /// <param name="resignDate">请求传入的离职日期</param>
    /// <param name="now">当前时间（UTC）</param>
    public static DateOnly? ResolveResignDate(EmployeeStatus status, DateOnly? resignDate, DateTimeOffset now)
    {
        if (status == EmployeeStatus.Active)
        {
            // 回到在职即清空离职日期，避免「在职却有离职日期」的自相矛盾
            return null;
        }

        return resignDate ?? DateOnly.FromDateTime(now.UtcDateTime);
    }
}
