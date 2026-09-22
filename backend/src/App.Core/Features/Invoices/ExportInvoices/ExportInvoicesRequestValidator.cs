using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Invoices.ExportInvoices;

/// <summary>
/// 发票导出请求格式校验：与列表查询规则完全一致（只做数据格式检查）
/// </summary>
public sealed class ExportInvoicesRequestValidator : AbstractValidator<ExportInvoicesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportInvoicesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 InvoiceFieldConstraints（禁止硬编码；覆盖发票号 / 往来名称 / 关联单据号的匹配列长）
        RuleFor(x => x.Keyword).MaximumLength(InvoiceFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.Type)
            .Must(t => t is null or InvoiceType.Purchase or InvoiceType.Sales)
            .WithMessage("发票类型取值非法");

        // 日期范围闭区间：两者都传时 start <= end
        RuleFor(x => new { x.Start, x.End })
            .Must(v => v.Start is null || v.End is null || v.End >= v.Start)
            .WithMessage("结束日期不能早于开始日期");
    }
}