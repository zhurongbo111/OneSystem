using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Notifications.GetNotifications;

/// <summary>
/// 站内信分页查询请求格式校验：长度 / 取值边界统一取自 <see cref="NotificationFieldConstraints"/>
/// 与 <see cref="NotificationType"/>（与 EF 配置一致，禁止硬编码）
/// </summary>
public sealed class GetNotificationsRequestValidator : AbstractValidator<GetNotificationsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetNotificationsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 起");

        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Keyword)
            .MaximumLength(NotificationFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {NotificationFieldConstraints.KeywordMaxLength} 个字符")
            .When(x => x.Keyword is not null);

        RuleFor(x => x.Type)
            .Must(type => type is not null && Enum.IsDefined((NotificationType)type.Value))
            .WithMessage("消息类型不合法")
            .When(x => x.Type is not null);
    }
}
