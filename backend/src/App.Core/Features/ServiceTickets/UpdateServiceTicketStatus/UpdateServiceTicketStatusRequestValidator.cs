using FluentValidation;

namespace App.Core.Features.ServiceTickets.UpdateServiceTicketStatus;

/// <summary>
/// 服务工单状态流转请求格式校验：只做数据格式检查（id 兜底、状态枚举取值）。
/// 状态流转白名单在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class UpdateServiceTicketStatusRequestValidator : AbstractValidator<UpdateServiceTicketStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateServiceTicketStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("工单 id 不能为空");
        RuleFor(x => x.Status).Must(s => Enum.IsDefined(s)).WithMessage("工单状态取值非法");
    }
}
