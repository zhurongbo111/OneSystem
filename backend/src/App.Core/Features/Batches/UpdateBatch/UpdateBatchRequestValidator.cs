using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Batches.UpdateBatch;

/// <summary>
/// 编辑批次请求格式校验：同创建（去掉批次号）；到期日与生产日期同日或晚于生产日期
/// </summary>
public sealed class UpdateBatchRequestValidator : AbstractValidator<UpdateBatchRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateBatchRequestValidator()
    {
        RuleFor(x => x.ExpiryDate)
            .GreaterThanOrEqualTo(x => x.ProductionDate!.Value)
            .WithMessage("到期日不能早于生产日期")
            .When(x => x.ExpiryDate is not null && x.ProductionDate is not null);

        RuleFor(x => x.Remark)
            .MaximumLength(OrderFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {OrderFieldConstraints.RemarkMaxLength} 个字符")
            .When(x => x.Remark is not null);
    }
}
