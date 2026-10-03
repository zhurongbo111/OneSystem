namespace App.Core.Entities;

/// <summary>
/// 批次字段约束的**单一来源**（specs/040-erp-batch-expiry/design.md §2.6）：
/// EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c> 均引用本类常量；
/// 备注长度对齐单据域，直接引用 <see cref="OrderFieldConstraints.RemarkMaxLength"/>（不在本类重复定义）。
/// </summary>
public static class BatchFieldConstraints
{
    /// <summary>批次号最短长度</summary>
    public const int BatchNoMinLength = 1;

    /// <summary>批次号最大长度（对齐 Batches.BatchNo varchar(50) 与单据明细 BatchNo 快照列长）</summary>
    public const int BatchNoMaxLength = 50;

    /// <summary>批次号格式：1–50 位字母 / 数字 / 下划线 / 连字符</summary>
    public const string BatchNoPattern = @"^[A-Za-z0-9_-]{1,50}$";

    /// <summary>近效期窗口天数（到期日 ≤ 今天 + 30 天且未过期 = 近效期；唯一来源）</summary>
    public const int NearExpiryDays = 30;

    /// <summary>批次列表查询关键词最大长度（对齐 BatchNo 列长）</summary>
    public const int KeywordMaxLength = 50;

    /// <summary>
    /// 将请求中的日期规范化为 **UTC 午夜**（offset=0，到日粒度，040 §5）。
    /// 必须走 <c>DateTime</c> 路径显式构造：<c>new DateTimeOffset(UtcDateTime.Date, TimeSpan.Zero)</c>。
    /// 若直接用 <c>DateTimeOffset?.Date</c>，返回的 <c>DateTimeOffset</c> 赋值给实体的 <c>DateTimeOffset?</c>
    /// 属性时经 <c>DateTime</c> 隐式转换按服务器本地时区解释（偏移漂移，如 +08:00），
    /// Npgsql 写入 timestamptz 报 50000。注意 <c>DateTimeOffset.Date</c> 返回 <c>DateTimeOffset</c>
    /// 而非 <c>DateTime</c>，传给 <c>DateTimeOffset</c> 构造器同样会本地化，故取 <c>UtcDateTime.Date</c>（<c>DateTime</c>）。
    /// <see cref="Batch"/> 的 ProductionDate / ExpiryDate 按 UTC 午夜存储。
    /// </summary>
    public static DateTimeOffset? NormalizeToDateUtcMidnight(DateTimeOffset? value)
        => value is null ? null : new DateTimeOffset(value.Value.UtcDateTime.Date, TimeSpan.Zero);
}
