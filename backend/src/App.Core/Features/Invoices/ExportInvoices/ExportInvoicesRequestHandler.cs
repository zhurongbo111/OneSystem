using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Invoices.ExportInvoices;

/// <summary>
/// 发票导出用例（erp-export 续行）：复用列表筛选取全量发票 + 一次批量取关联明细分组，
/// 输出「单据 + 明细」两个工作表（明细首列为所属发票号）；作废发票照常导出，便于对账核对
/// </summary>
public sealed class ExportInvoicesRequestHandler : IRequestHandler<ExportInvoicesRequest, ExportResultDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化发票导出用例处理器
    /// </summary>
    public ExportInvoicesRequestHandler(
        IInvoiceRepository invoiceRepository,
        IExcelExporter excelExporter)
    {
        _invoiceRepository = invoiceRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理发票导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportInvoicesRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（导出当前筛选全量，分页参数不参与）
        var (invoices, _) = await _invoiceRepository.GetPagedAsync(
            request.Keyword,
            request.Type,
            request.PartnerId,
            request.Start,
            request.End,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(invoices.Count);

        // 关联明细一次批量取回（避免逐单查询 N+1），再按发票 id 分组
        var invoiceIds = invoices.Select(v => v.Id).ToList();
        var items = await _invoiceRepository.GetItemsByInvoiceIdsAsync(invoiceIds, cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var itemsByInvoice = items.ToLookup(i => i.InvoiceId);

        var invoiceRows = new List<IReadOnlyList<object?>>(invoices.Count);
        var itemRows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var invoice in invoices)
        {
            invoiceRows.Add(new object?[]
            {
                invoice.InvoiceNo,
                ExportLabels.ToText(invoice.Type),
                invoice.PartnerName,
                invoice.InvoiceDate,
                invoice.AmountExcludingTax,
                ExportLabels.ToPercentage(invoice.TaxRate),
                invoice.TaxAmount,
                invoice.TotalAmount,
                ExportLabels.ToText(invoice.Status),
                invoice.CreatedAt,
            });

            foreach (var item in itemsByInvoice[invoice.Id])
            {
                itemRows.Add(new object?[]
                {
                    invoice.InvoiceNo,
                    ExportLabels.ToText(item.OrderType),
                    item.OrderNo,
                    item.OrderDate,
                    item.OrderTotalAmount,
                    item.Amount,
                });
            }
        }

        var workbook = new ExcelWorkbookModel
        {
            Sheets =
            [
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DocumentSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "发票号", ValueType = ExcelValueType.Text, Width = 22 },
                        new ExcelColumnModel { Header = "类型", ValueType = ExcelValueType.Text, Width = 10 },
                        new ExcelColumnModel { Header = "往来单位", ValueType = ExcelValueType.Text, Width = 24 },
                        new ExcelColumnModel { Header = "开票日期", ValueType = ExcelValueType.Date, Width = 14 },
                        new ExcelColumnModel { Header = "不含税金额", ValueType = ExcelValueType.Decimal, Width = 16 },
                        new ExcelColumnModel { Header = "税率", ValueType = ExcelValueType.Text, Width = 10 },
                        new ExcelColumnModel { Header = "税额", ValueType = ExcelValueType.Decimal, Width = 14 },
                        new ExcelColumnModel { Header = "价税合计", ValueType = ExcelValueType.Decimal, Width = 16 },
                        new ExcelColumnModel { Header = "状态", ValueType = ExcelValueType.Text, Width = 10 },
                        new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                    ],
                    Rows = invoiceRows,
                },
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DetailSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "发票号", ValueType = ExcelValueType.Text, Width = 22 },
                        new ExcelColumnModel { Header = "单据类型", ValueType = ExcelValueType.Text, Width = 14 },
                        new ExcelColumnModel { Header = "单据号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "单据日期", ValueType = ExcelValueType.Date, Width = 14 },
                        new ExcelColumnModel { Header = "单据金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                        new ExcelColumnModel { Header = "本次开票金额", ValueType = ExcelValueType.Decimal, Width = 16 },
                    ],
                    Rows = itemRows,
                },
            ],
        };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.Invoices, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}