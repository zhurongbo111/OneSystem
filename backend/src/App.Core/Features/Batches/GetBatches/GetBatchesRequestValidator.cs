using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Batches.GetBatches;

/// <summary>
/// 批次分页查询请求格式校验：长度 / 取值边界统一取自 BatchFieldConstraints（与 EF 配置一致，禁止硬编码）
/// </summary>
public sealed class GetBatchesRequestValidator : AbstractValidator<GetBatchesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetBatchesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 起");

        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Keyword)
            .MaximumLength(BatchFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {BatchFieldConstraints.KeywordMaxLength} 个字符")
            .When(x => x.Keyword is not null);

        RuleFor(x => x.Status)
            .Must(s => s is 0 or 1)
            .WithMessage("批次状态必须为 0（停用）/ 1（启用）")
            .When(x => x.Status is not null);
    }
}
