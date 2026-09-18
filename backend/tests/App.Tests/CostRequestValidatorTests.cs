using App.Core.Entities;
using App.Core.Features.Costs.RecalculateCosts;
using App.Core.Features.Reports;
using App.Core.Features.StockTakes.CreateStockTake;

namespace App.Tests;

/// <summary>
/// 成本相关请求格式校验测试（specs/026-erp-cost design.md §3.5）：
/// 期初成本单价必填与区间、盘点模式禁止传成本、重算期间先后与 366 天上限（与报表同源）。
/// </summary>
public class CostRequestValidatorTests
{
    private static readonly DateTimeOffset Date = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CreateStockTakeRequest Request(StockTakeType type, int? quantity = null, decimal? unitCost = null)
        => new()
        {
            Type = type,
            TakeDate = Date,
            Items =
            [
                new CreateStockTakeItem
                {
                    ProductId = Guid.NewGuid(),
                    ActualQuantity = quantity ?? 10,
                    UnitCost = unitCost,
                },
            ],
        };

    [Fact]
    public void 期初建账_成本单价必填()
    {
        var validator = new CreateStockTakeRequestValidator();

        Assert.False(validator.Validate(Request(StockTakeType.Initial)).IsValid);
        Assert.True(validator.Validate(Request(StockTakeType.Initial, unitCost: 10m)).IsValid);
    }

    [Fact]
    public void 期初建账_成本单价边界应与商品单价同源()
    {
        var validator = new CreateStockTakeRequestValidator();

        Assert.True(validator.Validate(Request(StockTakeType.Initial, unitCost: ProductFieldConstraints.PriceMinValue)).IsValid);
        Assert.True(validator.Validate(Request(StockTakeType.Initial, unitCost: ProductFieldConstraints.PriceMaxValue)).IsValid);
        Assert.False(validator.Validate(Request(StockTakeType.Initial, unitCost: ProductFieldConstraints.PriceMaxValue + 0.01m)).IsValid);
        Assert.False(validator.Validate(Request(StockTakeType.Initial, unitCost: -0.01m)).IsValid);
    }

    [Fact]
    public void 库存盘点_传入成本单价应被拒绝()
    {
        var validator = new CreateStockTakeRequestValidator();

        // 盘点按当时移动加权均价处理，不允许传成本单价（避免误传入库成本）
        Assert.False(validator.Validate(Request(StockTakeType.Take, unitCost: 10m)).IsValid);
        Assert.True(validator.Validate(Request(StockTakeType.Take)).IsValid);
    }

    [Fact]
    public void 成本重算_期间先后与上限应与报表同源()
    {
        var validator = new RecalculateCostsRequestValidator();

        Assert.True(validator.Validate(new RecalculateCostsRequest()).IsValid);

        Assert.False(validator.Validate(new RecalculateCostsRequest
        {
            Start = Date.AddDays(2),
            End = Date.AddDays(1),
        }).IsValid);

        var maxEnd = Date.AddDays(ReportFieldConstraints.MaxRangeDays);
        Assert.True(validator.Validate(new RecalculateCostsRequest { Start = Date, End = maxEnd }).IsValid);
        Assert.False(validator.Validate(new RecalculateCostsRequest { Start = Date, End = maxEnd.AddDays(1) }).IsValid);
    }
}
