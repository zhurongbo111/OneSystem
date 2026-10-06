using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Opportunities.CreateOpportunity;

/// <summary>
/// 新增商机请求格式校验：只做数据格式检查（长度 / 枚举取值 / 金额区间）。
/// 线索 / 客户 / 负责人存在性等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateOpportunityRequestValidator : AbstractValidator<CreateOpportunityRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateOpportunityRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("商机名称不能为空")
            .MaximumLength(OpportunityFieldConstraints.NameMaxLength).WithMessage("商机名称超长");
        RuleFor(x => x.Remark).MaximumLength(OpportunityFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");

        RuleFor(x => x.Amount)
            .InclusiveBetween(0m, OpportunityFieldConstraints.AmountMaxValue)
            .WithMessage("预计金额必须在 0 到允许上限之间");

        RuleFor(x => x.Stage).Must(s => Enum.IsDefined(s)).WithMessage("商机阶段取值非法");
    }
}
