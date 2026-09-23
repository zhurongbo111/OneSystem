using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Exports;
using App.Core.Features.Products.ExportProducts;
using App.Core.Features.PurchaseReceipts.ExportPurchaseReceipts;
using App.Core.Features.Reports;
using App.Core.Features.Reports.ExportInventoryFlow;
using App.Core.Features.Reports.ExportPurchaseSummary;
using App.Core.Features.Reports.ExportSalesSummary;
using App.Infrastructure.Exports;

using ClosedXML.Excel;

namespace App.Tests;

/// <summary>
/// 导出用例 Handler 测试（erp-export）：筛选 / 分页传参透传、导出上限（40000）与边界、
/// 列集与枚举文案、单据两工作表与明细归属、报表合计行、空结果仅表头、文件名格式。
/// </summary>
public class ExportRequestHandlerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 17, 10, 30, 0, TimeSpan.Zero);

    private static XLWorkbook Open(ExportResultDto result) => new(new MemoryStream(result.Content));

    // ============================== 商品列表（单工作表） ==============================

    private static (ExportProductsRequestHandler Handler, RecordingProductRepository Products, RecordingUserRepository Users) CreateProductsHandler()
    {
        var products = new RecordingProductRepository();
        var users = new RecordingUserRepository();
        return (new ExportProductsRequestHandler(products, users, new ClosedXmlExcelExporter()), products, users);
    }

    private static ProductListItem NewProductItem(Guid? createdBy = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = "sku-01",
            Name = "商品一",
            CategoryId = Guid.NewGuid(),
            CategoryName = "原材料",
            Unit = "个",
            PurchasePrice = 10.5m,
            SalePrice = 20m,
            SafetyStock = 5,
            StockQuantity = 3,
            Status = ProductStatus.Enabled,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime,
            CreatedBy = createdBy,
        };

    [Fact]
    public async Task 导出商品_按上限取全量并透传筛选_创建人换显示名()
    {
        var creatorId = Guid.NewGuid();
        var (handler, products, users) = CreateProductsHandler();
        products.Items = [NewProductItem(creatorId)];
        users.DisplayNames[creatorId] = "管理员";

        var categoryId = Guid.NewGuid();
        var result = await handler.HandleAsync(new ExportProductsRequest
        {
            Keyword = "sku",
            CategoryId = categoryId,
            Status = ProductStatus.Enabled,
            Page = 3,
            PageSize = 50,
        });

        // 分页参数不参与取数：恒 page = 1、pageSize = 上限 + 1（用于超限判定）
        var args = products.LastPagedArgs!.Value;
        Assert.Equal("sku", args.Keyword);
        Assert.Equal(categoryId, args.CategoryId);
        Assert.Equal(ProductStatus.Enabled, args.Status);
        Assert.Equal(1, args.Page);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, args.PageSize);
        Assert.Equal([creatorId], users.LastDisplayNameIds);

        Assert.Matches(@"^商品_\d{12}\.xlsx$", result.FileName);

        using var workbook = Open(result);
        var sheet = workbook.Worksheet("商品");
        Assert.Equal(
            ["商品编码", "商品名称", "分类", "单位", "采购价", "销售价", "库存", "安全库存", "状态", "创建时间", "创建人"],
            Enumerable.Range(1, 11).Select(i => sheet.Cell(1, i).GetString()));
        Assert.Equal("sku-01", sheet.Cell(2, 1).GetString());
        Assert.Equal("商品一", sheet.Cell(2, 2).GetString());
        Assert.Equal("原材料", sheet.Cell(2, 3).GetString());
        Assert.Equal("个", sheet.Cell(2, 4).GetString());
        Assert.Equal(10.5, sheet.Cell(2, 5).GetDouble(), 4);
        Assert.Equal(20, sheet.Cell(2, 6).GetDouble(), 4);
        Assert.Equal(3, sheet.Cell(2, 7).GetValue<int>());
        Assert.Equal(5, sheet.Cell(2, 8).GetValue<int>());
        Assert.Equal("启用", sheet.Cell(2, 9).GetString());
        // 时间按服务器本地时区输出（与前端展示口径一致），断言用同一转换避免依赖运行机器的时区
        Assert.Equal(BaseTime.LocalDateTime, sheet.Cell(2, 10).GetDateTime());
        Assert.Equal("管理员", sheet.Cell(2, 11).GetString());
    }

    [Fact]
    public async Task 导出商品_超过上限_抛参数错误()
    {
        var (handler, products, _) = CreateProductsHandler();
        products.Items = Enumerable.Range(0, ExportFieldConstraints.MaxRows + 1)
            .Select(_ => NewProductItem())
            .ToList();

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ExportProductsRequest()));

        Assert.Equal(ErrorCode.Validation, exception.Code);
        Assert.Contains("50000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 导出商品_恰好上限_通过()
    {
        var (handler, products, _) = CreateProductsHandler();
        products.Items = Enumerable.Range(0, ExportFieldConstraints.MaxRows)
            .Select(_ => NewProductItem())
            .ToList();

        var result = await handler.HandleAsync(new ExportProductsRequest());

        using var workbook = Open(result);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, workbook.Worksheet("商品").LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task 导出商品_空结果_仅输出表头()
    {
        var (handler, _, _) = CreateProductsHandler();

        var result = await handler.HandleAsync(new ExportProductsRequest());

        using var workbook = Open(result);
        var sheet = workbook.Worksheet("商品");
        Assert.Equal("商品编码", sheet.Cell(1, 1).GetString());
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
    }

    // ============================== 采购入库单（单据 + 明细两工作表） ==============================

    private static (ExportPurchaseReceiptsRequestHandler Handler, RecordingPurchaseReceiptRepository Receipts, RecordingProductRepository Products, RecordingUserRepository Users) CreateReceiptsHandler()
    {
        var receipts = new RecordingPurchaseReceiptRepository();
        var products = new RecordingProductRepository();
        var users = new RecordingUserRepository();
        return (new ExportPurchaseReceiptsRequestHandler(receipts, products, users, new ClosedXmlExcelExporter()), receipts, products, users);
    }

    [Fact]
    public async Task 导出采购入库_产出单据与明细两表_明细首列为单号且一次批量取明细()
    {
        var creatorId = Guid.NewGuid();
        var (handler, receipts, products, users) = CreateReceiptsHandler();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        receipts.Orders =
        [
            new PurchaseReceipt
            {
                Id = orderId,
                ReceiptNo = "GR202609170001",
                PartnerId = Guid.NewGuid(),
                PartnerName = "供应商一",
                OrderDate = BaseTime,
                TotalAmount = 300m,
                SettledAmount = 100m,
                Status = OrderStatus.Normal,
                CreatedAt = BaseTime,
                UpdatedAt = BaseTime,
                CreatedBy = creatorId,
            },
            new PurchaseReceipt
            {
                Id = Guid.NewGuid(),
                ReceiptNo = "GR202609170002",
                PartnerId = Guid.NewGuid(),
                PartnerName = "供应商二",
                OrderDate = BaseTime,
                TotalAmount = 50m,
                Status = OrderStatus.Voided,
                CreatedAt = BaseTime,
                UpdatedAt = BaseTime,
            },
        ];
        receipts.Items =
        [
            new PurchaseReceiptItem
            {
                Id = Guid.NewGuid(),
                ReceiptId = orderId,
                ProductId = productId,
                ProductName = "商品一",
                Unit = "个",
                Quantity = 3,
                UnitPrice = 100m,
                Subtotal = 300m,
            },
        ];
        products.Codes[productId] = "sku-01";
        users.DisplayNames[creatorId] = "管理员";

        var result = await handler.HandleAsync(new ExportPurchaseReceiptsRequest { Keyword = "GR" });

        Assert.Equal(1, receipts.LastPagedArgs!.Value.Page);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, receipts.LastPagedArgs.Value.PageSize);
        // 明细一次批量取回（不逐单查询）
        Assert.Equal(1, receipts.ItemsCallCount);
        Assert.Matches(@"^采购入库_\d{12}\.xlsx$", result.FileName);

        using var workbook = Open(result);
        Assert.Equal(["单据", "明细"], workbook.Worksheets.Select(w => w.Name));

        var documents = workbook.Worksheet("单据");
        Assert.Equal(
            ["单号", "关联订单", "供应商", "仓库", "单据日期", "总金额", "结算状态", "单据状态", "创建时间", "创建人"],
            Enumerable.Range(1, 10).Select(i => documents.Cell(1, i).GetString()));
        Assert.Equal("GR202609170001", documents.Cell(2, 1).GetString());
        Assert.Equal("供应商一", documents.Cell(2, 3).GetString());
        // 部分结算：与列表同口径（含未结金额）
        Assert.Equal("部分结算（未结 200.00）", documents.Cell(2, 7).GetString());
        Assert.Equal("正常", documents.Cell(2, 8).GetString());
        Assert.Equal("管理员", documents.Cell(2, 10).GetString());
        // 作废单据照常导出
        Assert.Equal("GR202609170002", documents.Cell(3, 1).GetString());
        Assert.Equal("已作废", documents.Cell(3, 8).GetString());

        var details = workbook.Worksheet("明细");
        Assert.Equal(
            ["单号", "商品编码", "商品名称", "单位", "数量", "单价", "小计"],
            Enumerable.Range(1, 7).Select(i => details.Cell(1, i).GetString()));
        Assert.Equal("GR202609170001", details.Cell(2, 1).GetString());
        Assert.Equal("sku-01", details.Cell(2, 2).GetString());
        Assert.Equal("商品一", details.Cell(2, 3).GetString());
        Assert.Equal(3, details.Cell(2, 5).GetValue<int>());
        Assert.Equal(300, details.Cell(2, 7).GetDouble(), 4);
    }

    [Fact]
    public async Task 导出采购入库_明细行数超过上限_抛参数错误()
    {
        var (handler, receipts, _, _) = CreateReceiptsHandler();
        var orderId = Guid.NewGuid();
        receipts.Orders =
        [
            new PurchaseReceipt
            {
                Id = orderId,
                ReceiptNo = "GR202609170001",
                PartnerId = Guid.NewGuid(),
                PartnerName = "供应商一",
                OrderDate = BaseTime,
                CreatedAt = BaseTime,
                UpdatedAt = BaseTime,
            },
        ];
        receipts.Items = Enumerable.Range(0, ExportFieldConstraints.MaxRows + 1)
            .Select(_ => new PurchaseReceiptItem { Id = Guid.NewGuid(), ReceiptId = orderId })
            .ToList();

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ExportPurchaseReceiptsRequest()));

        Assert.Equal(ErrorCode.Validation, exception.Code);
    }

    // ============================== 进销存报表（含合计行） ==============================

    [Fact]
    public async Task 导出进销存报表_按上限取全量并输出合计行()
    {
        var repository = new FakeReportQueryRepository
        {
            InventoryFlowItems =
            [
                new InventoryFlowItem
                {
                    ProductId = Guid.NewGuid(),
                    Code = "sku-01",
                    Name = "商品一",
                    CategoryName = "原材料",
                    Unit = "个",
                    OpeningQuantity = 10,
                    InboundQuantity = 5,
                    OutboundQuantity = 3,
                    ClosingQuantity = 12,
                },
            ],
            InventoryFlowTotal = 1,
            InventoryFlowSummary = new InventoryFlowTotal
            {
                OpeningQuantity = 10,
                InboundQuantity = 5,
                OutboundQuantity = 3,
                ClosingQuantity = 12,
            },
        };
        var handler = new ExportInventoryFlowRequestHandler(repository, new ClosedXmlExcelExporter());

        var result = await handler.HandleAsync(new ExportInventoryFlowRequest
        {
            Start = BaseTime.AddDays(-7),
            End = BaseTime,
            OnlyChanged = true,
        });

        var args = repository.LastInventoryFlowArgs!.Value;
        Assert.Equal(BaseTime.AddDays(-7), args.Start);
        Assert.Equal(BaseTime, args.End);
        Assert.True(args.OnlyChanged);
        Assert.Equal(1, args.Page);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, args.PageSize);
        Assert.Matches(@"^进销存报表_\d{12}\.xlsx$", result.FileName);

        using var workbook = Open(result);
        var sheet = workbook.Worksheet("进销存报表");
        Assert.Equal(
            ["商品编码", "商品名称", "分类", "单位", "期初数量", "期间入", "期间出", "期末数量"],
            Enumerable.Range(1, 8).Select(i => sheet.Cell(1, i).GetString()));
        Assert.Equal("sku-01", sheet.Cell(2, 1).GetString());
        Assert.Equal(12, sheet.Cell(2, 8).GetValue<int>());

        // 合计行紧随数据行，仅汇总数量列
        Assert.Equal("合计", sheet.Cell(3, 1).GetString());
        Assert.Equal(10, sheet.Cell(3, 5).GetValue<int>());
        Assert.Equal(5, sheet.Cell(3, 6).GetValue<int>());
        Assert.Equal(3, sheet.Cell(3, 7).GetValue<int>());
        Assert.Equal(12, sheet.Cell(3, 8).GetValue<int>());
    }

    [Fact]
    public async Task 导出进销存报表_超过上限_抛参数错误()
    {
        var repository = new FakeReportQueryRepository
        {
            InventoryFlowItems = Enumerable.Range(0, ExportFieldConstraints.MaxRows + 1)
                .Select(_ => new InventoryFlowItem
                {
                    ProductId = Guid.NewGuid(),
                    Code = "sku",
                    Name = "商品",
                    CategoryName = "分类",
                    Unit = "个",
                    OpeningQuantity = 0,
                    InboundQuantity = 0,
                    OutboundQuantity = 0,
                    ClosingQuantity = 0,
                })
                .ToList(),
        };
        var handler = new ExportInventoryFlowRequestHandler(repository, new ClosedXmlExcelExporter());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new ExportInventoryFlowRequest
        {
            Start = BaseTime.AddDays(-1),
            End = BaseTime,
        }));

        Assert.Equal(ErrorCode.Validation, exception.Code);
    }

    // ============================== 汇总报表：单位列不留空 ==============================

    [Fact]
    public async Task 导出采购汇总_往来单位维度_单位列输出占位符而非空白()
    {
        var repository = new FakeReportQueryRepository
        {
            PurchaseSummaryItems =
            [
                new PurchaseSummaryItem
                {
                    Key = Guid.NewGuid(),
                    Name = "供应商一",
                    Unit = null,
                    OrderCount = 2,
                    InboundQuantity = 5,
                    InboundAmount = 50m,
                    ReturnQuantity = 1,
                    ReturnAmount = 10m,
                },
            ],
            PurchaseSummaryTotal = 1,
            PurchaseSummarySummary = new PurchaseSummaryTotal
            {
                OrderCount = 2,
                InboundQuantity = 5,
                InboundAmount = 50m,
                ReturnQuantity = 1,
                ReturnAmount = 10m,
            },
        };
        var handler = new ExportPurchaseSummaryRequestHandler(repository, new ClosedXmlExcelExporter());

        var result = await handler.HandleAsync(new ExportPurchaseSummaryRequest
        {
            Start = BaseTime.AddDays(-7),
            End = BaseTime,
            GroupBy = SummaryGroupBy.Partner,
        });

        Assert.False(repository.LastPurchaseSummaryArgs!.Value.GroupByProduct);

        using var workbook = Open(result);
        var sheet = workbook.Worksheet("采购汇总");
        Assert.Equal("供应商", sheet.Cell(1, 1).GetString());
        // 往来维度聚合多种商品、无单一单位 → 输出占位符（不留空白单元格）
        Assert.Equal("-", sheet.Cell(2, 2).GetString());
        Assert.Equal(2, sheet.Cell(2, 3).GetValue<int>());
        Assert.Equal("合计", sheet.Cell(3, 1).GetString());
        Assert.Equal("-", sheet.Cell(3, 2).GetString());
    }

    [Fact]
    public async Task 导出销售汇总_商品维度_单位列输出商品单位()
    {
        var repository = new FakeReportQueryRepository
        {
            SalesSummaryItems =
            [
                new SalesSummaryItem
                {
                    Key = Guid.NewGuid(),
                    Name = "商品一",
                    Unit = "个",
                    OrderCount = 1,
                    OutboundQuantity = 2,
                    OutboundAmount = 40m,
                    ReturnQuantity = 0,
                    ReturnAmount = 0m,
                },
            ],
            SalesSummaryTotal = 1,
            SalesSummarySummary = new SalesSummaryTotal
            {
                OrderCount = 1,
                OutboundQuantity = 2,
                OutboundAmount = 40m,
                ReturnQuantity = 0,
                ReturnAmount = 0m,
            },
        };
        var handler = new ExportSalesSummaryRequestHandler(repository, new ClosedXmlExcelExporter());

        var result = await handler.HandleAsync(new ExportSalesSummaryRequest
        {
            Start = BaseTime.AddDays(-7),
            End = BaseTime,
            GroupBy = SummaryGroupBy.Product,
        });

        Assert.True(repository.LastSalesSummaryArgs!.Value.GroupByProduct);

        using var workbook = Open(result);
        var sheet = workbook.Worksheet("销售汇总");
        Assert.Equal("商品", sheet.Cell(1, 1).GetString());
        Assert.Equal("个", sheet.Cell(2, 2).GetString());
    }
}
