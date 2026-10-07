using FluentValidation;

namespace App.Core.Features.ServiceTickets.AssignServiceTicket;

/// <summary>
/// 指派服务工单负责人请求格式校验：只做数据格式检查（id / 负责人 id 非空）。
/// 「已关闭不可指派」与负责人存在性在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class AssignServiceTicketRequestValidator : AbstractValidator<AssignServiceTicketRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public AssignServiceTicketRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("工单 id 不能为空");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("负责人不能为空");
    }
}
