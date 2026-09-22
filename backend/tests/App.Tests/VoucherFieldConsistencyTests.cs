using App.Core.Auth;
using App.Core.Entities;
using App.Core.Features.Vouchers.CreateVoucher;
using App.Core.Features.Vouchers.GetVouchers;
using App.Core.Finance;
using App.Infrastructure;
using App.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// 总账字段约束一致性测试（specs/033-erp-general-ledger tasks 6.6）：
/// ① 实体 EF 实际列长 == 对应常量；② 凭证查询关键词上限 == 实际匹配列长；
/// ③ 手工凭证校验按 摘要 / 分录条数 / 金额区间 / 恰一方向 的常量边界通过或拒绝；
/// ④ 会计期间与科目映射种子幂等
/// </summary>
public class VoucherFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    [Fact]
    public void EF模型_Vouchers与VoucherEntries列长_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(VoucherFieldConstraints.NoMaxLength, GetMaxLength<Voucher>(dbContext, nameof(Voucher.VoucherNo)));
        Assert.Equal(VoucherFieldConstraints.SummaryMaxLength, GetMaxLength<Voucher>(dbContext, nameof(Voucher.Summary)));
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<Voucher>(dbContext, nameof(Voucher.SourceNo)));

        Assert.Equal(AccountFieldConstraints.CodeMaxLength, GetMaxLength<VoucherEntry>(dbContext, nameof(VoucherEntry.AccountCode)));
        Assert.Equal(AccountFieldConstraints.NameMaxLength, GetMaxLength<VoucherEntry>(dbContext, nameof(VoucherEntry.AccountName)));
        Assert.Equal(VoucherFieldConstraints.EntrySummaryMaxLength, GetMaxLength<VoucherEntry>(dbContext, nameof(VoucherEntry.Summary)));

        Assert.Equal(AccountMappingFieldConstraints.KeyMaxLength, GetMaxLength<AccountMapping>(dbContext, nameof(AccountMapping.Key)));

        // 金额列类型：numeric(18,2)
        var amountColumnType = $"numeric({VoucherFieldConstraints.AmountPrecision},{VoucherFieldConstraints.AmountDecimalPlaces})";
        Assert.Equal(amountColumnType, GetColumnType<Voucher>(dbContext, nameof(Voucher.TotalDebit)));
        Assert.Equal(amountColumnType, GetColumnType<Voucher>(dbContext, nameof(Voucher.TotalCredit)));
        Assert.Equal(amountColumnType, GetColumnType<VoucherEntry>(dbContext, nameof(VoucherEntry.Debit)));
        Assert.Equal(amountColumnType, GetColumnType<VoucherEntry>(dbContext, nameof(VoucherEntry.Credit)));
    }

    /// <summary>读 EF 注解中的列类型（InMemory 提供程序无法解析关系型映射，见 FieldValidationConsistencyTests）</summary>
    private static string? GetColumnType<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!
            .FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;

    [Fact]
    public void 凭证查询关键词长度_应不超过凭证号列长()
    {
        var ok = new string('a', VoucherFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', VoucherFieldConstraints.KeywordMaxLength + 1);
        var validator = new GetVouchersRequestValidator();

        Assert.True(validator.Validate(new GetVouchersRequest { Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetVouchersRequest { Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 凭证摘要长度_边界值通过越界拒绝()
    {
        var summaryOk = new string('摘', VoucherFieldConstraints.SummaryMaxLength);
        var summaryTooLong = new string('摘', VoucherFieldConstraints.SummaryMaxLength + 1);
        var validator = new CreateVoucherRequestValidator();

        Assert.True(validator.Validate(VoucherRequest(summaryOk)).IsValid);
        Assert.False(validator.Validate(VoucherRequest(summaryTooLong)).IsValid);
        Assert.False(validator.Validate(VoucherRequest(string.Empty)).IsValid);
    }

    [Fact]
    public void 凭证分录条数_边界值通过越界拒绝()
    {
        var validator = new CreateVoucherRequestValidator();

        Assert.True(validator.Validate(VoucherRequest("摘要", VoucherFieldConstraints.EntriesMaxCount)).IsValid);
        Assert.False(validator.Validate(VoucherRequest("摘要", VoucherFieldConstraints.EntriesMaxCount + 1)).IsValid);
        Assert.False(validator.Validate(VoucherRequest("摘要", 0)).IsValid);
    }

    [Fact]
    public void 分录金额与借贷方向_按常量边界校验()
    {
        var validator = new CreateVoucherRequestValidator();

        // 边界：上界通过、越界拒绝
        Assert.True(validator.Validate(VoucherRequest("摘要", 1, VoucherFieldConstraints.PerEntryMaxAmount, 0m)).IsValid);
        Assert.False(validator.Validate(VoucherRequest("摘要", 1, VoucherFieldConstraints.PerEntryMaxAmount + 0.01m, 0m)).IsValid);

        // 借方与贷方恰有一个大于 0
        Assert.False(validator.Validate(VoucherRequest("摘要", 1, 0m, 0m)).IsValid);
        Assert.False(validator.Validate(VoucherRequest("摘要", 1, 10m, 10m)).IsValid);
        Assert.True(validator.Validate(VoucherRequest("摘要", 1, 0m, 10m)).IsValid);
    }

    [Fact]
    public void 记账日期为空_应拒绝()
    {
        var request = new CreateVoucherRequest
        {
            VoucherDate = default,
            Summary = "摘要",
            Items = [new CreateVoucherItem { AccountId = Guid.NewGuid(), Debit = 1m }],
        };

        Assert.False(new CreateVoucherRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public async Task 会计期间与科目映射种子_应幂等()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        using var services = CreateInitializerServices(dbContext);

        await DatabaseInitializer.InitializeAsync(services, applyMigrations: false);

        var year = DateTimeOffset.UtcNow.Year;
        Assert.Equal(12, await dbContext.AccountingPeriods.CountAsync(p => p.Year == year));
        Assert.All(
            await dbContext.AccountingPeriods.ToListAsync(),
            period => Assert.Equal(PeriodStatus.Open, period.Status));

        var mappings = await dbContext.AccountMappings.ToListAsync();
        Assert.Equal(AccountMappingKeys.All.Count, mappings.Count);
        Assert.Equal(
            AccountMappingKeys.All.Select(d => d.Key).ToHashSet(StringComparer.Ordinal),
            mappings.Select(m => m.Key).ToHashSet(StringComparer.Ordinal));

        // 映射指向 031 预置科目（按编码解析）
        var codes = await dbContext.Accounts.Select(a => a.Code).ToListAsync();
        var mappedAccountIds = mappings.Select(m => m.AccountId).ToHashSet();
        var mappedAccounts = await dbContext.Accounts.Where(a => mappedAccountIds.Contains(a.Id)).Select(a => a.Code).ToListAsync();
        Assert.All(mappedAccounts, code => Assert.Contains(code, codes));

        // 幂等：重复执行不重复写入
        await DatabaseInitializer.InitializeAsync(services, applyMigrations: false);
        Assert.Equal(12, await dbContext.AccountingPeriods.CountAsync(p => p.Year == year));
        Assert.Equal(AccountMappingKeys.All.Count, await dbContext.AccountMappings.CountAsync());
    }

    private static CreateVoucherRequest VoucherRequest(string summary, int entryCount = 1, decimal debit = 100m, decimal credit = 0m)
        => new()
        {
            VoucherDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            Summary = summary,
            Items = Enumerable.Range(0, entryCount)
                .Select(_ => new CreateVoucherItem { AccountId = Guid.NewGuid(), Debit = debit, Credit = credit })
                .ToList(),
        };

    /// <summary>构建种子初始化所需的服务容器（InMemory 库 + 密码哈希组件，等价 Program 的注册）</summary>
    private static ServiceProvider CreateInitializerServices(AppDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton<PasswordHasher>(TestSupport.PasswordHasher);
        return services.BuildServiceProvider();
    }
}
