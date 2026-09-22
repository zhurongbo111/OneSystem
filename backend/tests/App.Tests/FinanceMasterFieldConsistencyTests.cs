using App.Core.Auth;
using App.Core.Entities;
using App.Core.Features.Accounts.CreateAccount;
using App.Core.Features.Accounts.UpdateAccount;
using App.Core.Features.TaxRates.CreateTaxRate;
using App.Core.Features.TaxRates.GetTaxRates;
using App.Core.Features.TaxRates.UpdateTaxRate;
using App.Infrastructure;
using App.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// 财务主数据字段约束一致性测试（specs/031-erp-finance-master tasks.md 5.4）：
/// ① 两实体 EF 实际列长 / 精度 == 对应常量；② 税率查询关键词上限 == 实际匹配列长；
/// ③ 税率区间 / 小数位、科目长度 / 区间在创建与编辑两处同源。另含预置科目种子幂等（tasks.md 5.3）。
/// </summary>
public class FinanceMasterFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_Accounts表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(AccountFieldConstraints.CodeMaxLength, GetMaxLength<Account>(dbContext, nameof(Account.Code)));
        Assert.Equal(AccountFieldConstraints.NameMaxLength, GetMaxLength<Account>(dbContext, nameof(Account.Name)));
        Assert.Equal(AccountFieldConstraints.RemarkMaxLength, GetMaxLength<Account>(dbContext, nameof(Account.Remark)));
    }

    [Fact]
    public void EF模型_TaxRates表列长度与税率精度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var rateProperty = dbContext.Model.FindEntityType(typeof(TaxRate))!.FindProperty(nameof(TaxRate.Rate))!;

        Assert.Equal(TaxRateFieldConstraints.CodeMaxLength, GetMaxLength<TaxRate>(dbContext, nameof(TaxRate.Code)));
        Assert.Equal(TaxRateFieldConstraints.NameMaxLength, GetMaxLength<TaxRate>(dbContext, nameof(TaxRate.Name)));
        Assert.Equal(TaxRateFieldConstraints.RemarkMaxLength, GetMaxLength<TaxRate>(dbContext, nameof(TaxRate.Remark)));
        Assert.Equal(TaxRateFieldConstraints.RatePrecision, rateProperty.GetPrecision());
        Assert.Equal(TaxRateFieldConstraints.RateDecimalPlaces, rateProperty.GetScale());
    }

    // ============================== 查询关键词上限 == 实际匹配列 ==============================

    [Fact]
    public void 税率查询关键词长度_应不超过名称列长()
    {
        var ok = new string('a', TaxRateFieldConstraints.NameMaxLength);
        var tooLong = new string('a', TaxRateFieldConstraints.NameMaxLength + 1);

        Assert.True(new GetTaxRatesRequestValidator()
            .Validate(new GetTaxRatesRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetTaxRatesRequestValidator()
            .Validate(new GetTaxRatesRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 税率区间 / 小数位（创建与编辑同源）==============================

    [Fact]
    public void 税率区间与小数位_创建与编辑应一致_边界值通过越界拒绝()
    {
        // 边界：0 与 100 通过；-1 与 101 拒绝
        Assert.True(ValidateCreateRate(TaxRateFieldConstraints.RateMin));
        Assert.True(ValidateCreateRate(TaxRateFieldConstraints.RateMax));
        Assert.True(ValidateUpdateRate(TaxRateFieldConstraints.RateMin));
        Assert.True(ValidateUpdateRate(TaxRateFieldConstraints.RateMax));

        Assert.False(ValidateCreateRate(TaxRateFieldConstraints.RateMin - 0.0001m));
        Assert.False(ValidateCreateRate(TaxRateFieldConstraints.RateMax + 0.0001m));
        Assert.False(ValidateUpdateRate(TaxRateFieldConstraints.RateMin - 0.0001m));
        Assert.False(ValidateUpdateRate(TaxRateFieldConstraints.RateMax + 0.0001m));

        // 小数位：4 位通过，5 位拒绝
        Assert.True(ValidateCreateRate(13.5678m));
        Assert.True(ValidateUpdateRate(13.5678m));
        Assert.False(ValidateCreateRate(13.56789m));
        Assert.False(ValidateUpdateRate(13.56789m));
    }

    [Fact]
    public void 税率编码名称长度_创建与编辑应一致()
    {
        var codeOk = new string('a', TaxRateFieldConstraints.CodeMaxLength);
        var nameOk = new string('名', TaxRateFieldConstraints.NameMaxLength);
        var nameTooLong = new string('名', TaxRateFieldConstraints.NameMaxLength + 1);

        Assert.True(new CreateTaxRateRequestValidator()
            .Validate(new CreateTaxRateRequest { Code = codeOk, Name = nameOk, Rate = 13m }).IsValid);
        Assert.True(new UpdateTaxRateRequestValidator()
            .Validate(new UpdateTaxRateRequest { Id = Guid.NewGuid(), Code = codeOk, Name = nameOk, Rate = 13m }).IsValid);

        Assert.False(new CreateTaxRateRequestValidator()
            .Validate(new CreateTaxRateRequest { Code = codeOk, Name = nameTooLong, Rate = 13m }).IsValid);
        Assert.False(new UpdateTaxRateRequestValidator()
            .Validate(new UpdateTaxRateRequest { Id = Guid.NewGuid(), Code = codeOk, Name = nameTooLong, Rate = 13m }).IsValid);
    }

    // ============================== 科目长度 / 排序区间（创建与编辑同源）==============================

    [Fact]
    public void 科目编码名称排序_创建与编辑应引用同一常量区间()
    {
        var codeOk = new string('a', AccountFieldConstraints.CodeMaxLength);
        var nameOk = new string('名', AccountFieldConstraints.NameMaxLength);

        Assert.True(new CreateAccountRequestValidator().Validate(new CreateAccountRequest
        {
            Code = codeOk,
            Name = nameOk,
            SortOrder = AccountFieldConstraints.SortOrderMaxValue,
        }).IsValid);

        Assert.False(new CreateAccountRequestValidator().Validate(new CreateAccountRequest
        {
            Code = codeOk,
            Name = nameOk,
            SortOrder = AccountFieldConstraints.SortOrderMaxValue + 1,
        }).IsValid);

        Assert.True(new UpdateAccountRequestValidator().Validate(new UpdateAccountRequest
        {
            Id = Guid.NewGuid(),
            Code = codeOk,
            Name = nameOk,
            SortOrder = AccountFieldConstraints.SortOrderMinValue,
        }).IsValid);

        // 类别 / 方向枚举取值非法应拒绝
        Assert.False(new CreateAccountRequestValidator().Validate(new CreateAccountRequest
        {
            Code = codeOk,
            Name = nameOk,
            Category = 9,
        }).IsValid);
        Assert.False(new UpdateAccountRequestValidator().Validate(new UpdateAccountRequest
        {
            Id = Guid.NewGuid(),
            Code = codeOk,
            Name = nameOk,
            Direction = 9,
        }).IsValid);
    }

    // ============================== 预置科目种子 ==============================

    [Fact]
    public async Task 预置科目种子_应幂等且全部标记为预置科目()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        using var services = CreateInitializerServices(dbContext);

        await DatabaseInitializer.InitializeAsync(services, applyMigrations: false);
        var accounts = await dbContext.Accounts.ToListAsync();

        Assert.Equal(15, accounts.Count);
        Assert.All(accounts, account => Assert.True(account.IsPreset));
        Assert.All(accounts, account => Assert.Equal(AccountStatus.Enabled, account.Status));
        Assert.All(accounts, account => Assert.Null(account.ParentId));

        // 类别与默认方向：资产 / 成本 → 借，负债 / 权益 / 损益 → 贷
        Assert.Contains(accounts, account => account.Code == "1001"
            && account.Category == AccountCategory.Asset
            && account.Direction == AccountDirection.Debit);
        Assert.Contains(accounts, account => account.Code == "2202"
            && account.Category == AccountCategory.Liability
            && account.Direction == AccountDirection.Credit);
        Assert.Contains(accounts, account => account.Code == "5001"
            && account.Category == AccountCategory.Cost
            && account.Direction == AccountDirection.Debit);
        Assert.Contains(accounts, account => account.Code == "6001"
            && account.Category == AccountCategory.ProfitLoss
            && account.Direction == AccountDirection.Credit);

        // 幂等：重复执行不重复写入
        await DatabaseInitializer.InitializeAsync(services, applyMigrations: false);
        Assert.Equal(15, await dbContext.Accounts.CountAsync());
    }

    private static bool ValidateCreateRate(decimal rate)
        => new CreateTaxRateRequestValidator()
            .Validate(new CreateTaxRateRequest { Code = "VAT13", Name = "增值税 13%", Rate = rate }).IsValid;

    private static bool ValidateUpdateRate(decimal rate)
        => new UpdateTaxRateRequestValidator()
            .Validate(new UpdateTaxRateRequest { Id = Guid.NewGuid(), Code = "VAT13", Name = "增值税 13%", Rate = rate }).IsValid;

    /// <summary>构建种子初始化所需的服务容器（InMemory 库 + 密码哈希组件，等价 Program 的注册）</summary>
    private static ServiceProvider CreateInitializerServices(AppDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton<PasswordHasher>(TestSupport.PasswordHasher);
        return services.BuildServiceProvider();
    }
}