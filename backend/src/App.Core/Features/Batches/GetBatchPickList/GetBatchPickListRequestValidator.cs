using FluentValidation;

namespace App.Core.Features.Batches.GetBatchPickList;

/// <summary>
/// 批次下拉查询请求格式校验：商品 / 仓库必填
/// </summary>
public sealed class GetBatchPickListRequestValidator : AbstractValidator<GetBatchPickListRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetBatchPickListRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("商品不能为空");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("仓库不能为空");
    }
}
