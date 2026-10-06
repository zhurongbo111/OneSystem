namespace App.Core.Entities;

/// <summary>
/// 告警去重台账实体（对应 PostgreSQL 表 AlertRecords，specs/041-erp-stock-alert/design.md §2.2）。
/// 唯一索引 <c>(AlertType, ResourceKey, AlertDate)</c> 保证「同一信号对象 + 类型同一天只告警一次」，
/// 并发重复扫描由数据库拒绝（按跳过处理）。台账只增不改、不做清理（保留期范围外）。
/// </summary>
public sealed class AlertRecord
{
    /// <summary>台账记录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>告警信号类型（见 <see cref="AlertType"/>）</summary>
    public AlertType AlertType { get; set; }

    /// <summary>业务对象键（去重键组成，见 design.md §0）</summary>
    public string ResourceKey { get; set; } = string.Empty;

    /// <summary>告警日期（UTC 日期粒度，去重键组成）</summary>
    public DateOnly AlertDate { get; set; }

    /// <summary>写入时间</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
