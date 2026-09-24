using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Transfers.GetTransfers;

/// <summary>
/// 调拨单分页查询请求格式校验：只做数据格式检查。
/// 长度 / 数量边界与采购 / 销售单据域同源（OrderFieldConstraints，后端规则 §5.3）。
/// </summary>
public sealed class GetTransfersRequestValidator : AbstractValidator<GetTransfersRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetTransfersRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 OrderFieldConstraints（与 TransferNo 列长一致）
        RuleFor(x => x.Keyword).MaximumLength(OrderFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        // 日期范围闭区间：两者都传时 start <= end（跨字段校验用匿名类型组合）
        RuleFor(x => new { x.Start, x.End })
            .Must(v => v.Start is null || v.End is null || v.End >= v.Start)
            .WithMessage("结束日期不能早于开始日期");
    }
}
