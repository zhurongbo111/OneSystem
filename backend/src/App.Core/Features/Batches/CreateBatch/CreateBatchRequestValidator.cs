using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Batches.CreateBatch;

/// <summary>
/// 新增批次请求格式校验：只做数据格式检查；商品是否存在 / 是否启用批次管理、批次号唯一等查库约束在 Handler 内。
/// 长度 / 取值边界统一取自 BatchFieldConstraints（与 EF 配置一致，禁止硬编码）。
/// </summary>
public sealed class CreateBatchRequestValidator : AbstractValidator<CreateBatchRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateBatchRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("商品不能为空");

        RuleFor(x => x.BatchNo)
            .NotEmpty().WithMessage("批次号不能为空")
            .Length(BatchFieldConstraints.BatchNoMinLength, BatchFieldConstraints.BatchNoMaxLength)
            .WithMessage($"批次号长度必须在 {BatchFieldConstraints.BatchNoMinLength} 到 {BatchFieldConstraints.BatchNoMaxLength} 之间")
            .Matches(BatchFieldConstraints.BatchNoPattern)
            .WithMessage("批次号只能包含字母、数字、下划线或连字符");

        // 到期日与生产日期同日或晚于生产日期（040 §2.2；两者均可空，仅同时提供时校验）
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
