using App.Core.Abstractions;

namespace App.Core.Features.Notifications.GetNotificationSummary;

/// <summary>
/// 顶栏未读汇总请求（无请求参数；空模型仅用于统一 RequestHandler 入口签名，不定义 Validator）
/// </summary>
public sealed class GetNotificationSummaryRequest : IRequest<NotificationSummaryDto>
{
}
