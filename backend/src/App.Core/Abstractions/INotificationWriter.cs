using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 站内信写入通道（实现见 App.Infrastructure；041-erp-stock-alert §1）。
/// 扫描器只依赖本接口产出消息，后续通知（如 `042` 的待审批提醒）复用同一通道。
/// </summary>
public interface INotificationWriter
{
    /// <summary>
    /// 批量写入站内信（空集合直接返回；调用方保证标题 / 内容不超列长——文案由固定模板生成）
    /// </summary>
    /// <param name="notifications">待写入消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default);
}
