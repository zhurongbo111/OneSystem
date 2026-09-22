using App.Core.Entities;
using App.Core.Errors;
using App.Core.Finance;

namespace App.Tests;

/// <summary>
/// <see cref="VoucherFactory"/> 测试（specs/033-erp-general-ledger tasks 6.1）：
/// 6 类来源 + 成本结转的分录（科目 / 借贷 / 金额）正确；收付款按结算方式选现金 / 银行科目；
/// 映射缺失抛 40158；借贷平衡由共享校验兜底
/// </summary>
public class VoucherFactoryTests
{
    private static readonly DateTimeOffset VoucherDate = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static VoucherDraft Build(
        VoucherSourceType sourceType,
        decimal totalAmount = 1000m,
        decimal costAmount = 0m,
        SettlementMethod? settlementMethod = null,
        IReadOnlyDictionary<string, Account>? accountsByKey = null)
        => VoucherFactory.Build(
            sourceType,
            Guid.NewGuid(),
            "GR202609010001",
            VoucherDate,
            totalAmount,
            costAmount,
            settlementMethod,
            accountsByKey ?? GeneralLedgerStubs.Create().AccountsByKey,
            Guid.NewGuid(),
            "记-202609-0001",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

    [Fact]
    public void 采购入库_应借存货贷应付账款()
    {
        var draft = Build(VoucherSourceType.PurchaseInbound, totalAmount: 1000m);

        Assert.Equal(2, draft.Entries.Count);
        AssertDebit(draft, 0, "1405", 1000m);
        AssertCredit(draft, 1, "2202", 1000m);
        Assert.Equal(1000m, draft.Voucher.TotalDebit);
        Assert.Equal(1000m, draft.Voucher.TotalCredit);
    }

    [Fact]
    public void 销售出库_应确认收入并同凭证结转成本()
    {
        var draft = Build(VoucherSourceType.SalesOutbound, totalAmount: 1500m, costAmount: 900m);

        // 借 应收账款 1500 / 贷 主营业务收入 1500；借 主营业务成本 900 / 贷 库存商品 900
        Assert.Equal(4, draft.Entries.Count);
        AssertDebit(draft, 0, "1122", 1500m);
        AssertCredit(draft, 1, "6001", 1500m);
        AssertDebit(draft, 2, "6401", 900m);
        AssertCredit(draft, 3, "1405", 900m);
        Assert.Equal(2400m, draft.Voucher.TotalDebit);
    }

    [Fact]
    public void 销售出库_无成本结转时只保留收入分录()
    {
        var draft = Build(VoucherSourceType.SalesOutbound, totalAmount: 1500m, costAmount: 0m);

        Assert.Equal(2, draft.Entries.Count);
        AssertDebit(draft, 0, "1122", 1500m);
        AssertCredit(draft, 1, "6001", 1500m);
    }

    [Fact]
    public void 采购退货_应借应付账款贷存货()
    {
        var draft = Build(VoucherSourceType.PurchaseReturn, totalAmount: 300m);

        Assert.Equal(2, draft.Entries.Count);
        AssertDebit(draft, 0, "2202", 300m);
        AssertCredit(draft, 1, "1405", 300m);
    }

    [Fact]
    public void 销售退货_应冲减收入并转回成本()
    {
        var draft = Build(VoucherSourceType.SalesReturn, totalAmount: 400m, costAmount: 250m);

        Assert.Equal(4, draft.Entries.Count);
        AssertDebit(draft, 0, "6001", 400m);
        AssertCredit(draft, 1, "1122", 400m);
        AssertDebit(draft, 2, "1405", 250m);
        AssertCredit(draft, 3, "6401", 250m);
    }

    [Theory]
    [InlineData(SettlementMethod.Cash, "1001")]
    [InlineData(SettlementMethod.BankTransfer, "1002")]
    [InlineData(SettlementMethod.Other, "1002")]
    public void 收款_应按结算方式选现金或银行科目(SettlementMethod method, string expectedCode)
    {
        var draft = Build(VoucherSourceType.Receipt, totalAmount: 500m, settlementMethod: method);

        Assert.Equal(2, draft.Entries.Count);
        AssertDebit(draft, 0, expectedCode, 500m);
        AssertCredit(draft, 1, "1122", 500m);
    }

    [Fact]
    public void 付款_应借应付账款贷现金或银行科目()
    {
        var draft = Build(VoucherSourceType.Payment, totalAmount: 700m, settlementMethod: SettlementMethod.Cash);

        Assert.Equal(2, draft.Entries.Count);
        AssertDebit(draft, 0, "2202", 700m);
        AssertCredit(draft, 1, "1001", 700m);
    }

    [Fact]
    public void 映射缺失_应报AccountMappingMissing()
    {
        var empty = new Dictionary<string, Account>(StringComparer.Ordinal);

        var ex = Assert.Throws<BusinessException>(() => Build(VoucherSourceType.PurchaseInbound, accountsByKey: empty));

        Assert.Equal(ErrorCode.AccountMappingMissing, ex.Code);
    }

    [Fact]
    public void 来源类型为手工凭证_应拒绝自动构建()
    {
        var ex = Assert.Throws<BusinessException>(() => Build(VoucherSourceType.Manual));

        Assert.Equal(ErrorCode.VoucherAccountInvalid, ex.Code);
    }

    [Fact]
    public void 凭证主表_应带来源快照与摘要()
    {
        var sourceId = Guid.NewGuid();
        var draft = VoucherFactory.Build(
            VoucherSourceType.PurchaseInbound,
            sourceId,
            "GR202609010001",
            VoucherDate,
            1000m,
            0m,
            null,
            GeneralLedgerStubs.Create().AccountsByKey,
            Guid.NewGuid(),
            "记-202609-0007",
            null,
            DateTimeOffset.UtcNow);

        Assert.Equal(VoucherSourceType.PurchaseInbound, draft.Voucher.SourceType);
        Assert.Equal(sourceId, draft.Voucher.SourceId);
        Assert.Equal("GR202609010001", draft.Voucher.SourceNo);
        Assert.Equal("采购入库单 GR202609010001", draft.Voucher.Summary);
        Assert.Equal(VoucherStatus.Posted, draft.Voucher.Status);
        VoucherAssertExtensions.AssertAllEntries(draft);
    }

    private static void AssertDebit(VoucherDraft draft, int index, string accountCode, decimal amount)
    {
        var entry = draft.Entries[index];
        Assert.Equal(accountCode, entry.AccountCode);
        Assert.Equal(amount, entry.Debit);
        Assert.Equal(0m, entry.Credit);
    }

    private static void AssertCredit(VoucherDraft draft, int index, string accountCode, decimal amount)
    {
        var entry = draft.Entries[index];
        Assert.Equal(accountCode, entry.AccountCode);
        Assert.Equal(0m, entry.Debit);
        Assert.Equal(amount, entry.Credit);
    }
}

/// <summary>凭证断言的公共扩展</summary>
internal static class VoucherAssertExtensions
{
    /// <summary>断言行号连续（1 起）且分录均挂在凭证上</summary>
    public static void AssertAllEntries(VoucherDraft draft)
    {
        Assert.Equal(Enumerable.Range(1, draft.Entries.Count), draft.Entries.Select(e => e.LineNo));
        Assert.All(draft.Entries, e => Assert.Equal(draft.Voucher.Id, e.VoucherId));
    }
}
