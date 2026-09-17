using App.Core.Entities;
using App.Core.Features.Settlements.CreateSettlement;
using App.Core.Features.Settlements.GetReconciliation;
using App.Core.Features.Settlements.GetSettlements;
using App.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 结算域字段约束一致性测试（specs/023-erp-settlement §2.5 / 后端规则 §5.3）：
/// EF 实际列长 / 列类型 == 单据域常量；核销金额边界（0.01 通过 / 0 拒绝 / 超上界拒绝）；
/// 核销明细行数上限、枚举合法值、同一单据不重复、查询关键词上限。
/// </summary>
public class SettlementFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    private static string? GetColumnType<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetColumnType();

    private static CreateSettlementRequest RequestWith(params CreateSettlementItem[] items)
        => new()
        {
            Type = SettlementType.Receipt,
            PartnerId = Guid.NewGuid(),
            SettlementDate = DateTimeOffset.UtcNow,
            Method = SettlementMethod.Cash,
            Items = items,
        };

    private static CreateSettlementItem Line(SettlementOrderType orderType, Guid orderId, decimal amount)
        => new() { OrderType = orderType, OrderId = orderId, Amount = amount };

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_Settlements表列长_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<Settlement>(dbContext, nameof(Settlement.SettlementNo)));
        Assert.Equal(PartnerFieldConstraints.NameMaxLength, GetMaxLength<Settlement>(dbContext, nameof(Settlement.PartnerName)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<Settlement>(dbContext, nameof(Settlement.Remark)));
    }

    [Fact]
    public void EF模型_SettlementItems表列长_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<SettlementItem>(dbContext, nameof(SettlementItem.OrderNo)));
    }

    [Fact]
    public void EF模型_结算金额列_应与单据金额口径一致()
    {
        // 列类型需 relational 提供程序才能读取：用 Npgsql 构建模型但不建立连接
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=app")
            .Options;
        using var dbContext = new AppDbContext(options);

        // 精度口径 numeric(18,2)：与四类单据金额列同源
        Assert.Equal("numeric(18,2)", GetColumnType<Settlement>(dbContext, nameof(Settlement.TotalAmount)));
        Assert.Equal("numeric(18,2)", GetColumnType<SettlementItem>(dbContext, nameof(SettlementItem.Amount)));
        Assert.Equal("numeric(18,2)", GetColumnType<SettlementItem>(dbContext, nameof(SettlementItem.OrderTotalAmount)));
        Assert.Equal("numeric(18,2)", GetColumnType<PurchaseReceipt>(dbContext, nameof(PurchaseReceipt.SettledAmount)));
        Assert.Equal("numeric(18,2)", GetColumnType<SalesShipment>(dbContext, nameof(SalesShipment.SettledAmount)));
        Assert.Equal("numeric(18,2)", GetColumnType<PurchaseReturn>(dbContext, nameof(PurchaseReturn.SettledAmount)));
        Assert.Equal("numeric(18,2)", GetColumnType<SalesReturn>(dbContext, nameof(SalesReturn.SettledAmount)));
    }

    // ============================== 核销金额边界 ==============================

    [Fact]
    public void 核销金额_边界值_应按常量判定()
    {
        var validator = new CreateSettlementRequestValidator();

        Assert.True(validator.Validate(RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 0.01m))).IsValid);
        Assert.True(validator.Validate(RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), ProductFieldConstraints.PriceMaxValue))).IsValid);

        Assert.False(validator.Validate(RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 0m))).IsValid);
        Assert.False(validator.Validate(RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), -1m))).IsValid);
        Assert.False(validator.Validate(RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), ProductFieldConstraints.PriceMaxValue + 0.01m))).IsValid);
    }

    // ============================== 明细行数 / 重复 / 枚举 ==============================

    [Fact]
    public void 核销明细行数_上限应等于常量()
    {
        var validator = new CreateSettlementRequestValidator();

        var max = Enumerable.Range(0, OrderFieldConstraints.ItemsMaxCount)
            .Select(_ => Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 1m))
            .ToArray();
        Assert.True(validator.Validate(RequestWith(max)).IsValid);

        // 超出上限：重复同一单据不允许，故用不同单据 id 构造超限行数
        var tooMany = Enumerable.Range(0, OrderFieldConstraints.ItemsMaxCount + 1)
            .Select(_ => Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 1m))
            .ToArray();
        Assert.False(validator.Validate(RequestWith(tooMany)).IsValid);
    }

    [Fact]
    public void 核销明细_同一单据重复_应拒绝()
    {
        var orderId = Guid.NewGuid();
        var request = RequestWith(
            Line(SettlementOrderType.SalesOutbound, orderId, 10m),
            Line(SettlementOrderType.SalesOutbound, orderId, 20m));

        Assert.False(new CreateSettlementRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void 收付款请求_枚举与关键词_应拒绝非法取值()
    {
        var validator = new CreateSettlementRequestValidator();

        var badType = RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 1m));
        Assert.False(validator.Validate(new CreateSettlementRequest
        {
            Type = (SettlementType)9,
            PartnerId = badType.PartnerId,
            SettlementDate = badType.SettlementDate,
            Method = badType.Method,
            Items = badType.Items,
        }).IsValid);

        Assert.False(validator.Validate(new CreateSettlementRequest
        {
            Type = SettlementType.Receipt,
            PartnerId = badType.PartnerId,
            SettlementDate = badType.SettlementDate,
            Method = (SettlementMethod)9,
            Items = badType.Items,
        }).IsValid);

        Assert.False(validator.Validate(RequestWith(Line((SettlementOrderType)9, Guid.NewGuid(), 1m))).IsValid);

        // 备注超长（对齐 Remark 列长）
        var tooLongRemark = RequestWith(Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 1m));
        Assert.False(validator.Validate(new CreateSettlementRequest
        {
            Type = tooLongRemark.Type,
            PartnerId = tooLongRemark.PartnerId,
            SettlementDate = tooLongRemark.SettlementDate,
            Method = tooLongRemark.Method,
            Items = tooLongRemark.Items,
            Remark = new string('备', OrderFieldConstraints.RemarkMaxLength + 1),
        }).IsValid);
    }

    [Fact]
    public void 查询关键词_上限应等于匹配列长()
    {
        // 收付款列表：keyword 匹配单号 / 往来名称 → 取单号列长
        var listOk = new GetSettlementsRequestValidator()
            .Validate(new GetSettlementsRequest { Keyword = new string('k', OrderFieldConstraints.KeywordMaxLength) }).IsValid;
        var listTooLong = new GetSettlementsRequestValidator()
            .Validate(new GetSettlementsRequest { Keyword = new string('k', OrderFieldConstraints.KeywordMaxLength + 1) }).IsValid;
        Assert.True(listOk);
        Assert.False(listTooLong);

        // 往来台账：keyword 匹配往来名称 → 取往来名称列长
        var reconciliationOk = new GetReconciliationRequestValidator()
            .Validate(new GetReconciliationRequest { Keyword = new string('k', PartnerFieldConstraints.KeywordMaxLength) }).IsValid;
        var reconciliationTooLong = new GetReconciliationRequestValidator()
            .Validate(new GetReconciliationRequest { Keyword = new string('k', PartnerFieldConstraints.KeywordMaxLength + 1) }).IsValid;
        Assert.True(reconciliationOk);
        Assert.False(reconciliationTooLong);
    }

    [Fact]
    public void 分页参数_越界应拒绝()
    {
        Assert.False(new GetSettlementsRequestValidator().Validate(new GetSettlementsRequest { Page = 0 }).IsValid);
        Assert.False(new GetSettlementsRequestValidator().Validate(new GetSettlementsRequest { PageSize = 101 }).IsValid);
        Assert.False(new GetReconciliationRequestValidator().Validate(new GetReconciliationRequest { PageSize = 0 }).IsValid);
    }
}
