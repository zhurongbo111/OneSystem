using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Exports;
using App.Core.Features.PartnerPrices.ExportPartnerPrices;
using App.Infrastructure.Exports;

using ClosedXML.Excel;

namespace App.Tests;

/// <summary>
/// 客户价格列表导出用例测试（erp-export）：筛选透传、忽略分页取全量、超 10000 行 → 40000、
/// 导出列与列表列定义一致（含协议价与商品销售价对比），契约见 specs/027-erp-export/design.md §0.1。
/// </summary>
public class PartnerPriceExportTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 23, 10, 30, 0, TimeSpan.Zero);

    private static (ExportPartnerPricesRequestHandler Handler, RecordingPartnerPriceRepository Prices) CreateHandler()
    {
        var prices = new RecordingPartnerPriceRepository();
        return (new ExportPartnerPricesRequestHandler(prices, new ClosedXmlExcelExporter()), prices);
    }

    private static PartnerPriceListItem NewItem()
        => new()
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            PartnerName = "客户甲",
            ProductId = Guid.NewGuid(),
            ProductCode = "sku-001",
            ProductName = "测试商品",
            Unit = "箱",
            Price = 95m,
            SalePrice = 120m,
            Remark = "年度协议",
            CreatedAt = BaseTime,
        };

    [Fact]
    public async Task 导出客户价格_应透传筛选并忽略分页且列头与列表一致()
    {
        var partnerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var (handler, prices) = CreateHandler();
        prices.Items = [NewItem()];

        var result = await handler.HandleAsync(new ExportPartnerPricesRequest
        {
            PartnerId = partnerId,
            ProductId = productId,
            Keyword = "客户甲",
            Page = 3,
            PageSize = 50,
        });

        // 分页参数不参与取数：恒取上限 + 1（用于超限判定）
        var args = prices.LastPagedArgs!.Value;
        Assert.Equal(partnerId, args.PartnerId);
        Assert.Equal(productId, args.ProductId);
        Assert.Equal("客户甲", args.Keyword);
        Assert.Equal(1, args.Page);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, args.PageSize);

        Assert.StartsWith($"客户价格_", result.FileName);
        Assert.EndsWith(".xlsx", result.FileName);

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal(ExportDomainNames.PartnerPrices, sheet.Name);

        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToArray();
        Assert.Equal(
            ["客户", "商品编码", "商品名称", "单位", "协议价", "商品销售价", "价差", "备注", "创建时间"],
            headers);

        var row = sheet.Row(2);
        Assert.Equal("客户甲", row.Cell(1).GetString());
        Assert.Equal("sku-001", row.Cell(2).GetString());
        Assert.Equal(95m, row.Cell(5).GetValue<decimal>());
        Assert.Equal(120m, row.Cell(6).GetValue<decimal>());
        Assert.Equal(-25m, row.Cell(7).GetValue<decimal>()); // 价差 = 协议价 − 销售价
        Assert.Equal("年度协议", row.Cell(8).GetString());
    }

    [Fact]
    public async Task 导出客户价格_超出上限_应报Validation()
    {
        var (handler, prices) = CreateHandler();
        prices.Items = Enumerable.Range(0, ExportFieldConstraints.MaxRows + 1).Select(_ => NewItem()).ToList();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new ExportPartnerPricesRequest()));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }
}
