using App.Core.Entities;
using FluentValidation;

namespace App.Core.Features.Inventory.GetInventory;

/// <summary>
/// 库存分页查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetInventoryRequestValidator : AbstractValidator<GetInventoryRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetInventoryRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 ProductFieldConstraints（禁止硬编码；与 Code / Name 列长一致）
        RuleFor(x => x.Keyword).MaximumLength(ProductFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);
    }
}
