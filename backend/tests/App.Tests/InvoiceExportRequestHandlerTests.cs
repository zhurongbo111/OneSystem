using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Exports;
using App.Core.Features.Invoices.ExportInvoices;
using App.Infrastructure.Exports;

using ClosedXML.Excel;

namespace App.Tests;

/// <summary>
/// 发票导出用例测试（erp-export 续行）：筛选 / 分页传参透传、导出上限（40000）、
/// 「单据 + 明细」两工作表与明细归属、税率百分比与状态文案、文件名格式。
/// </summary>
public class InvoiceExportRequestHandlerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 22, 10, 30, 0, TimeSpan.Zero);

    private static XLWorkbook Open(ExportResultDto result) => new(new MemoryStream(result.Content));

    private static ExportInvoicesRequestHandler CreateHandler(FakeInvoiceRepository repository)
        => new(repository, new ClosedXmlExcelExporter());

    [Fact]
    public async Task 导出发票_产出单据与明细两表_明细首列为发票号且一次批量取明细()
    {
        var invoiceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var repository = new FakeInvoiceRepository
        {
            PagedItems =
            [
                new InvoiceListItem
                {
                    Id = invoiceId,
                    InvoiceNo = "12345678",
                    Type = InvoiceType.Sales,
                    PartnerName = "客户一",
                    InvoiceDate = BaseTime,
                    AmountExcludingTax = 900m,
                    TaxRate = 0.13m,
                    TaxAmount = 117m,
                    TotalAmount = 1017m,
                    Status = OrderStatus.Normal,
                    CreatedAt = BaseTime,
                    OrderNoSummary = "GI202609220001",
                },
            ],
            PagedTotal = 1,
        };

        var result = await CreateHandler(repository).HandleAsync(new ExportInvoicesRequest
        {
            Keyword = "12345678",
            Type = InvoiceType.Sales,
            PartnerId = Guid.NewGuid(),
            Start = BaseTime.AddDays(-7),
            End = BaseTime,
            Page = 3,
            PageSize = 50,
        });

        // 筛选透传；分页参数不参与取数：恒 page = 1、pageSize = 上限 + 1（用于超限判定）
        var args = repository.PagedQueries[^1];
        Assert.Equal("12345678", args.Keyword);
        Assert.Equal(InvoiceType.Sales, args.Type);
        Assert.Equal(BaseTime.AddDays(-7), args.Start);
        Assert.Equal(BaseTime, args.End);
        Assert.Equal(1, args.Page);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, args.PageSize);

        // 明细一次批量取回（不逐单查询）
        Assert.Equal([1], repository.ItemBatchSizes);
        Assert.Matches(@"^发票_\d{12}\.xlsx$", result.FileName);

        // 明细无数据时该表仅表头（两表恒存在）
        using var workbook = Open(result);
        Assert.Equal(["单据", "明细"], workbook.Worksheets.Select(w => w.Name));

        var documents = workbook.Worksheet("单据");
        Assert.Equal(
            ["发票号", "类型", "往来单位", "开票日期", "不含税金额", "税率", "税额", "价税合计", "状态", "创建时间"],
            Enumerable.Range(1, 10).Select(i => documents.Cell(1, i).GetString()));
        Assert.Equal("12345678", documents.Cell(2, 1).GetString());
        Assert.Equal("销项", documents.Cell(2, 2).GetString());
        Assert.Equal("客户一", documents.Cell(2, 3).GetString());
        Assert.Equal(900m, documents.Cell(2, 5).GetValue<decimal>());
        Assert.Equal("13%", documents.Cell(2, 6).GetString());
        Assert.Equal(117m, documents.Cell(2, 7).GetValue<decimal>());
        Assert.Equal(1017m, documents.Cell(2, 8).GetValue<decimal>());
        Assert.Equal("正常", documents.Cell(2, 9).GetString());

        var details = workbook.Worksheet("明细");
        Assert.Equal(
            ["发票号", "单据类型", "单据号", "单据日期", "单据金额", "本次开票金额"],
            Enumerable.Range(1, 6).Select(i => details.Cell(1, i).GetString()));
        Assert.Equal(1, details.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task 导出发票_明细归属与枚举文案正确()
    {
        var invoiceId = Guid.NewGuid();
        var repository = new FakeInvoiceRepository
        {
            PagedItems =
            [
                new InvoiceListItem
                {
                    Id = invoiceId,
                    InvoiceNo = "INV-1",
                    Type = InvoiceType.Purchase,
                    PartnerName = "供应商一",
                    InvoiceDate = BaseTime,
                    AmountExcludingTax = 100m,
                    TaxRate = 0.09m,
                    TaxAmount = 9m,
                    TotalAmount = 109m,
                    Status = OrderStatus.Voided,
                    CreatedAt = BaseTime,
                    OrderNoSummary = "GR202609220001、PR202609220002",
                },
            ],
            PagedTotal = 1,
        };
        repository.Seed(
            new Invoice { Id = invoiceId, InvoiceNo = "INV-1" },
            [
                new InvoiceItem
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    InvoiceId = invoiceId,
                    OrderType = SettlementOrderType.PurchaseInbound,
                    OrderId = Guid.NewGuid(),
                    OrderNo = "GR202609220001",
                    OrderDate = BaseTime,
                    OrderTotalAmount = 200m,
                    Amount = 100m,
                },
                new InvoiceItem
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    InvoiceId = invoiceId,
                    OrderType = SettlementOrderType.PurchaseReturn,
                    OrderId = Guid.NewGuid(),
                    OrderNo = "PR202609220002",
                    OrderDate = BaseTime,
                    OrderTotalAmount = 50m,
                    Amount = 30m,
                },
            ]);

        var result = await CreateHandler(repository).HandleAsync(new ExportInvoicesRequest());

        using var workbook = Open(result);
        var documents = workbook.Worksheet("单据");
        Assert.Equal("进项", documents.Cell(2, 2).GetString());
        Assert.Equal("9%", documents.Cell(2, 6).GetString());
        Assert.Equal("已作废", documents.Cell(2, 9).GetString());

        var details = workbook.Worksheet("明细");
        Assert.Equal("INV-1", details.Cell(2, 1).GetString());
        Assert.Equal("采购入库单", details.Cell(2, 2).GetString());
        Assert.Equal("GR202609220001", details.Cell(2, 3).GetString());
        Assert.Equal(200m, details.Cell(2, 5).GetValue<decimal>());
        Assert.Equal(100m, details.Cell(2, 6).GetValue<decimal>());
        Assert.Equal("采购退货单", details.Cell(3, 2).GetString());
        Assert.Equal("PR202609220002", details.Cell(3, 3).GetString());
        Assert.Equal(30m, details.Cell(3, 6).GetValue<decimal>());
    }

    [Fact]
    public async Task 导出发票_超过上限_抛参数错误()
    {
        var repository = new FakeInvoiceRepository
        {
            PagedItems = Enumerable.Range(0, ExportFieldConstraints.MaxRows + 1)
                .Select(_ => new InvoiceListItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceNo = "INV",
                    Type = InvoiceType.Sales,
                    PartnerName = "客户",
                    InvoiceDate = BaseTime,
                    AmountExcludingTax = 1m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    TotalAmount = 1m,
                    Status = OrderStatus.Normal,
                    CreatedAt = BaseTime,
                    OrderNoSummary = string.Empty,
                })
                .ToList(),
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(repository).HandleAsync(new ExportInvoicesRequest()));

        Assert.Equal(ErrorCode.Validation, exception.Code);
        Assert.Contains("50000", exception.Message, StringComparison.Ordinal);
    }
}