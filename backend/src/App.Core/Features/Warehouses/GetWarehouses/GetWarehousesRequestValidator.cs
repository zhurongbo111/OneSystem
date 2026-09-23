using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Warehouses.GetWarehouses;

/// <summary>
/// 仓库分页查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetWarehousesRequestValidator : AbstractValidator<GetWarehousesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetWarehousesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 WarehouseFieldConstraints（禁止硬编码）
        RuleFor(x => x.Keyword)
            .MaximumLength(WarehouseFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {WarehouseFieldConstraints.KeywordMaxLength} 个字符")
            .When(x => x.Keyword is not null);

        RuleFor(x => x.Status)
            .Must(s => s is null || s is PartnerStatus.Enabled or PartnerStatus.Disabled)
            .WithMessage("仓库状态值无效");
    }
}
