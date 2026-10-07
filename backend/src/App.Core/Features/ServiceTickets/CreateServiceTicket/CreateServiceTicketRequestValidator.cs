using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.ServiceTickets.CreateServiceTicket;

/// <summary>
/// 登记服务工单请求格式校验：只做数据格式检查（长度 / 枚举取值）。
/// 客户与负责人存在性等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateServiceTicketRequestValidator : AbstractValidator<CreateServiceTicketRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateServiceTicketRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("工单标题不能为空")
            .MaximumLength(ServiceTicketFieldConstraints.TitleMaxLength).WithMessage("工单标题超长");

        RuleFor(x => x.Description)
            .MaximumLength(ServiceTicketFieldConstraints.DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage("问题描述超长");
        RuleFor(x => x.Contact)
            .MaximumLength(ServiceTicketFieldConstraints.ContactMaxLength).When(x => x.Contact is not null)
            .WithMessage("联系人超长");
        RuleFor(x => x.Phone)
            .MaximumLength(ServiceTicketFieldConstraints.PhoneMaxLength).When(x => x.Phone is not null)
            .WithMessage("联系电话超长");
        RuleFor(x => x.Remark)
            .MaximumLength(ServiceTicketFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");

        RuleFor(x => x.Priority).Must(p => Enum.IsDefined(p)).WithMessage("优先级取值非法");
    }
}
