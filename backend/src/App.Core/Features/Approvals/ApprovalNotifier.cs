using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;

using Microsoft.Extensions.Logging;

using PermissionKeys = App.Core.Auth.Permissions;

namespace App.Core.Features.Approvals;

/// <summary>
/// 审批站内信通知器（specs/042-erp-approval/design.md §0.3）：待审批提醒发给具备
/// <c>approvals.approve</c> 权限的启用用户（复用 <see cref="IPermissionedUserQuery"/>），
/// 审批结果通知提交人。文案与降级策略收敛在此一处：**发信失败只记日志，不影响单据与审批状态**。
/// </summary>
public sealed class ApprovalNotifier
{
    private readonly INotificationWriter _notificationWriter;
    private readonly IPermissionedUserQuery _permissionedUserQuery;
    private readonly ILogger<ApprovalNotifier> _logger;

    /// <summary>
    /// 初始化审批站内信通知器
    /// </summary>
    public ApprovalNotifier(
        INotificationWriter notificationWriter,
        IPermissionedUserQuery permissionedUserQuery,
        ILogger<ApprovalNotifier> logger)
    {
        _notificationWriter = notificationWriter;
        _permissionedUserQuery = permissionedUserQuery;
        _logger = logger;
    }

    /// <summary>
    /// 提交审批后提醒具备审批权限的用户（点击跳转审批页并定位该条记录）
    /// </summary>
    /// <param name="approval">审批记录（含单号 / 往来 / 金额快照与提交时间）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task NotifyPendingAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        try
        {
            var userIds = await _permissionedUserQuery.GetEnabledUserIdsByPermissionAsync(
                PermissionKeys.ApprovalsApprove, cancellationToken);
            if (userIds.Count == 0)
            {
                return;
            }

            var typeText = AuditText.SettlementOrderType(approval.OrderType);
            var content = $"{typeText} {approval.OrderNo}（{approval.PartnerName}，{AuditSummary.Money(approval.Amount)}）待审批，请及时处理";
            var notifications = userIds
                .Select(userId => new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Type = NotificationType.ApprovalPending,
                    Title = "待审批单据",
                    Content = content,
                    LinkRouteName = "approvals",
                    LinkQuery = $"{{\"id\":\"{approval.Id}\"}}",
                    ResourceKey = ApprovalResourceKey(approval.Id),
                    CreatedAt = approval.SubmittedAt,
                })
                .ToList();

            await _notificationWriter.WriteAsync(notifications, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送待审批站内信失败：审批记录 {ApprovalId}、单据 {OrderNo}", approval.Id, approval.OrderNo);
        }
    }

    /// <summary>
    /// 审批完成后通知提交人（通过 / 驳回）
    /// </summary>
    /// <param name="approval">审批记录（含审批人 / 时间 / 意见）</param>
    /// <param name="decisionText">决定文案（「通过」/「驳回」）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task NotifyDecidedAsync(
        Approval approval, string decisionText, CancellationToken cancellationToken = default)
    {
        try
        {
            var typeText = AuditText.SettlementOrderType(approval.OrderType);
            var remark = string.IsNullOrWhiteSpace(approval.DecisionRemark)
                ? string.Empty
                : $"，审批意见：{approval.DecisionRemark.Trim()}";
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = approval.SubmittedBy,
                Type = NotificationType.ApprovalDecided,
                Title = "审批结果",
                Content = $"{typeText} {approval.OrderNo}（{approval.PartnerName}，{AuditSummary.Money(approval.Amount)}）已{decisionText}{remark}",
                ResourceKey = ApprovalResourceKey(approval.Id),
                CreatedAt = approval.DecidedAt ?? approval.SubmittedAt,
            };

            await _notificationWriter.WriteAsync([notification], cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送审批结果站内信失败：审批记录 {ApprovalId}、单据 {OrderNo}", approval.Id, approval.OrderNo);
        }
    }

    private static string ApprovalResourceKey(Guid approvalId) => $"approval:{approvalId}";
}
