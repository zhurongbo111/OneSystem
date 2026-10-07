using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.ServiceTickets.UpdateServiceTicket;

/// <summary>
/// 编辑服务工单请求格式校验：只做数据格式检查（长度 / 枚举取值）。
/// 存在性与「已关闭不可编辑」等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class UpdateServiceTicketRequestValidator : AbstractValidator<UpdateServiceTicketRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateServiceTicketRequestValidator()
    {
        // 路由参数不得从请求体绑定（后端规则 §4.2）：Id 可缺省，Controller 以路由值覆盖，此处仅兜底
        RuleFor(x => x.Id).NotEmpty().WithMessage("工单 id 不能为空");
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
