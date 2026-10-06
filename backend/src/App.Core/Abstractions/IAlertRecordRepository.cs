using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 告警去重台账仓储接口（实现见 App.Infrastructure；041-erp-stock-alert）。
/// 台账只增不改：<see cref="ExistsAsync"/> 供扫描器判重，<see cref="AddRangeAsync"/> 在消息写入之后落台账。
/// </summary>
public interface IAlertRecordRepository
{
    /// <summary>
    /// 判断「信号类型 + 业务对象键 + 告警日期」当日是否已告警
    /// </summary>
    /// <param name="alertType">告警信号类型</param>
    /// <param name="resourceKey">业务对象键（design.md §0）</param>
    /// <param name="alertDate">告警日期（UTC 日期粒度）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(
        AlertType alertType,
        string resourceKey,
        DateOnly alertDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量写入台账（消息写入成功之后调用；库层唯一索引为并发兜底）
    /// </summary>
    /// <param name="records">待写入台账</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddRangeAsync(IReadOnlyList<AlertRecord> records, CancellationToken cancellationToken = default);
}
