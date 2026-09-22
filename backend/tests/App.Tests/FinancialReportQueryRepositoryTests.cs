using App.Core.Entities;
using App.Core.Features.FinancialReports.GetAccountBalance;
using App.Core.Features.FinancialReports.GetBalanceSheet;
using App.Core.Features.FinancialReports.GetIncomeStatement;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 财务报表口径测试（specs/033-erp-general-ledger tasks 6.5；真实 <see cref="FinancialReportQueryRepository"/> + InMemory 只读场景）：
/// 科目余额表「期初 + 发生额 = 期末」按方向折算；资产负债表恒等式（资产 = 负债 + 权益 + 本年利润）；
/// 利润表收入 − 成本 = 毛利（与 `026` 成本毛利报表同源口径）；作废凭证不计入取数
/// </summary>
public class FinancialReportQueryRepositoryTests
{
    private static readonly DateTimeOffset PeriodStart = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static Account NewAccount(string code, string name, AccountCategory category, AccountDirection direction)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = category,
            Direction = direction,
            IsPreset = true,
            Status = AccountStatus.Enabled,
            CreatedAt = PeriodStart,
            UpdatedAt = PeriodStart,
        };

    private static (Voucher Voucher, List<VoucherEntry> Entries) NewVoucher(
        DateTimeOffset voucherDate,
        VoucherStatus status,
        params (Account Debit, Account Credit, decimal Amount)[] lines)
    {
        var voucherId = Guid.NewGuid();
        var entries = new List<VoucherEntry>();
        var lineNo = 1;
        foreach (var (debitAccount, creditAccount, amount) in lines)
        {
            entries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(),
                VoucherId = voucherId,
                LineNo = lineNo++,
                AccountId = debitAccount.Id,
                AccountCode = debitAccount.Code,
                AccountName = debitAccount.Name,
                Debit = amount,
                Credit = 0m,
            });
            entries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(),
                VoucherId = voucherId,
                LineNo = lineNo++,
                AccountId = creditAccount.Id,
                AccountCode = creditAccount.Code,
                AccountName = creditAccount.Name,
                Debit = 0m,
                Credit = amount,
            });
        }

        var voucher = new Voucher
        {
            Id = voucherId,
            VoucherNo = $"记-{voucherDate:yyyyMM}-{Guid.NewGuid().ToString("N")[..4]}",
            VoucherDate = voucherDate,
            PeriodId = Guid.NewGuid(),
            Summary = "测试凭证",
            SourceType = VoucherSourceType.Manual,
            TotalDebit = entries.Sum(e => e.Debit),
            TotalCredit = entries.Sum(e => e.Credit),
            Status = status,
            CreatedAt = voucherDate,
            UpdatedAt = voucherDate,
        };

        return (voucher, entries);
    }

    /// <summary>构造「采购入库 1000 + 销售出库 1500（成本 900）」的完整账簿（含 8 月末期初）</summary>
    private static async Task<(AppDbContext Context, Account Inventory, Account Payable, Account Receivable, Account Revenue, Account Cost)> SeedLedgerAsync()
    {
        var context = TestSupport.CreateDbContext();
        var inventory = NewAccount("1405", "库存商品", AccountCategory.Asset, AccountDirection.Debit);
        var payable = NewAccount("2202", "应付账款", AccountCategory.Liability, AccountDirection.Credit);
        var receivable = NewAccount("1122", "应收账款", AccountCategory.Asset, AccountDirection.Debit);
        var revenue = NewAccount("6001", "主营业务收入", AccountCategory.ProfitLoss, AccountDirection.Credit);
        var cost = NewAccount("6401", "主营业务成本", AccountCategory.ProfitLoss, AccountDirection.Credit);
        context.Accounts.AddRange(inventory, payable, receivable, revenue, cost);

        var vouchers = new List<Voucher>();
        var entries = new List<VoucherEntry>();

        // 采购入库：借 存货 1000 / 贷 应付账款 1000
        var inbound = NewVoucher(PeriodStart.AddDays(1), VoucherStatus.Posted, (inventory, payable, 1000m));
        // 销售出库：借 应收账款 1500 / 贷 收入 1500
        var outbound = NewVoucher(PeriodStart.AddDays(2), VoucherStatus.Posted, (receivable, revenue, 1500m));
        // 成本结转：借 成本 900 / 贷 存货 900
        var carry = NewVoucher(PeriodStart.AddDays(2), VoucherStatus.Posted, (cost, inventory, 900m));
        // 作废凭证：借 存货 500 / 贷 应付账款 500（不应计入取数）
        var voided = NewVoucher(PeriodStart.AddDays(3), VoucherStatus.Voided, (inventory, payable, 500m));

        foreach (var (voucher, voucherEntries) in new[] { inbound, outbound, carry, voided })
        {
            vouchers.Add(voucher);
            entries.AddRange(voucherEntries);
        }

        context.Vouchers.AddRange(vouchers);
        context.VoucherEntries.AddRange(entries);
        await context.SaveChangesAsync();

        return (context, inventory, payable, receivable, revenue, cost);
    }

    [Fact]
    public async Task 科目余额表_应按方向折算期初发生额与期末且排除作废凭证()
    {
        var (context, inventory, payable, receivable, revenue, cost) = await SeedLedgerAsync();
        var repository = new FinancialReportQueryRepository(context);

        var items = await repository.GetAccountBalancesAsync(2026, 9);
        var byCode = items.ToDictionary(i => i.Code);

        // 存货（借方科目）：本期借 1000、贷 900 → 期末 100
        Assert.Equal(0m, byCode["1405"].OpeningBalance);
        Assert.Equal(1000m, byCode["1405"].PeriodDebit);
        Assert.Equal(900m, byCode["1405"].PeriodCredit);
        Assert.Equal(100m, byCode["1405"].ClosingBalance);

        // 应付账款（贷方科目）：期末 1000（作废凭证的 500 不计入）
        Assert.Equal(1000m, byCode["2202"].ClosingBalance);

        // 应收账款（借方科目）：期末 1500
        Assert.Equal(1500m, byCode["1122"].ClosingBalance);

        // 收入（贷方科目）：期末 1500；成本（贷方科目、只有借方发生）→ 期末 −900
        Assert.Equal(1500m, byCode["6001"].ClosingBalance);
        Assert.Equal(-900m, byCode["6401"].ClosingBalance);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task 资产负债表_应满足资产等于负债加权益加本年利润()
    {
        var (context, _, _, _, _, _) = await SeedLedgerAsync();
        var repository = new FinancialReportQueryRepository(context);

        var items = await repository.GetBalanceSheetAsync(2026, 9);
        var totalAssets = items.Where(i => i.Category is AccountCategory.Asset or AccountCategory.Cost).Sum(i => i.Amount);
        var totalLiabilities = items.Where(i => i.Category == AccountCategory.Liability).Sum(i => i.Amount);
        var totalEquities = items.Where(i => i.Category == AccountCategory.Equity).Sum(i => i.Amount);
        var currentProfit = items.Where(i => i.Category == AccountCategory.ProfitLoss).Sum(i => i.Amount);

        Assert.Equal(1600m, totalAssets);          // 100 存货 + 1500 应收
        Assert.Equal(1000m, totalLiabilities);     // 应付账款
        Assert.Equal(0m, totalEquities);
        Assert.Equal(600m, currentProfit);         // 1500 收入 − 900 成本
        Assert.Equal(totalAssets, totalLiabilities + totalEquities + currentProfit);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task 利润表_收入减成本应等于毛利()
    {
        var (context, _, _, _, _, _) = await SeedLedgerAsync();
        var repository = new FinancialReportQueryRepository(context);
        var handler = new GetIncomeStatementRequestHandler(repository);

        var result = await handler.HandleAsync(new GetIncomeStatementRequest { Year = 2026, Month = 9 });

        Assert.Equal(1500m, result.TotalRevenue);
        Assert.Equal(900m, result.TotalCost);
        Assert.Equal(600m, result.NetProfit);
        Assert.Equal("6001", Assert.Single(result.RevenueItems).Code);
        Assert.Equal("6401", Assert.Single(result.CostItems).Code);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task 科目余额表与资产负债表_应通过用例映射出参()
    {
        var (context, _, _, _, _, _) = await SeedLedgerAsync();
        var repository = new FinancialReportQueryRepository(context);

        var balance = await new GetAccountBalanceRequestHandler(repository)
            .HandleAsync(new GetAccountBalanceRequest { Year = 2026, Month = 9 });
        var sheet = await new GetBalanceSheetRequestHandler(repository)
            .HandleAsync(new GetBalanceSheetRequest { Year = 2026, Month = 9 });

        Assert.NotEmpty(balance);
        Assert.Equal(1600m, sheet.TotalAssets);
        Assert.Equal(sheet.TotalAssets, sheet.TotalLiabilitiesAndEquity);
        Assert.Equal(600m, sheet.CurrentProfit);
        Assert.All(sheet.Assets, item => Assert.False(string.IsNullOrEmpty(item.Code)));

        await context.DisposeAsync();
    }

    [Fact]
    public async Task 期初余额_应取期间前已过账分录净额()
    {
        var (context, inventory, payable, _, _, _) = await SeedLedgerAsync();
        // 8 月已有一笔「借 存货 300 / 贷 应付账款 300」，应计入 9 月期初
        var (voucher, entries) = NewVoucher(new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero), VoucherStatus.Posted, (inventory, payable, 300m));
        context.Vouchers.Add(voucher);
        context.VoucherEntries.AddRange(entries);
        await context.SaveChangesAsync();

        var repository = new FinancialReportQueryRepository(context);
        var items = await repository.GetAccountBalancesAsync(2026, 9);

        Assert.Equal(300m, items.Single(i => i.Code == "1405").OpeningBalance);
        Assert.Equal(400m, items.Single(i => i.Code == "1405").ClosingBalance);
        Assert.Equal(300m, items.Single(i => i.Code == "2202").OpeningBalance);

        await context.DisposeAsync();
    }
}
