using App.Core.Auth;
using App.Core.Entities;
using App.Core.Features.BankAccounts.CreateBankAccount;
using App.Core.Features.BankAccounts.GetBankAccounts;
using App.Core.Features.BankAccounts.UpdateBankAccount;
using App.Infrastructure;
using App.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// 资金出纳字段约束一致性测试（specs/034-erp-cash tasks.md 6.4 / 6.5）：
/// ① EF 实际列长 / 精度 == <see cref="BankAccountFieldConstraints"/> 常量；
/// ② 账户编码 / 名称 / 开户行 / 账号在创建与编辑两处同源，边界值通过、越界拒绝；
/// ③ 查询关键词上限 == 实际匹配列长；④ 预置现金账户种子幂等。
/// </summary>
public class CashFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_BankAccounts表列长度与金额精度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var balanceProperty = dbContext.Model
            .FindEntityType(typeof(BankAccount))!
            .FindProperty(nameof(BankAccount.InitialBalance))!;

        Assert.Equal(BankAccountFieldConstraints.CodeMaxLength, GetMaxLength<BankAccount>(dbContext, nameof(BankAccount.Code)));
        Assert.Equal(BankAccountFieldConstraints.NameMaxLength, GetMaxLength<BankAccount>(dbContext, nameof(BankAccount.Name)));
        Assert.Equal(BankAccountFieldConstraints.BankNameMaxLength, GetMaxLength<BankAccount>(dbContext, nameof(BankAccount.BankName)));
        Assert.Equal(BankAccountFieldConstraints.AccountNoMaxLength, GetMaxLength<BankAccount>(dbContext, nameof(BankAccount.AccountNo)));
        Assert.Equal(BankAccountFieldConstraints.RemarkMaxLength, GetMaxLength<BankAccount>(dbContext, nameof(BankAccount.Remark)));
        Assert.Equal(BankAccountFieldConstraints.AmountPrecision, balanceProperty.GetPrecision());
        Assert.Equal(BankAccountFieldConstraints.AmountDecimalPlaces, balanceProperty.GetScale());
    }

    // ============================== 查询关键词上限 == 实际匹配列 ==============================

    [Fact]
    public void 资金账户查询关键词长度_应不超过名称列长()
    {
        var ok = new string('a', BankAccountFieldConstraints.NameMaxLength);
        var tooLong = new string('a', BankAccountFieldConstraints.NameMaxLength + 1);

        Assert.True(new GetBankAccountsRequestValidator()
            .Validate(new GetBankAccountsRequest { Keyword = ok }).IsValid);
        Assert.False(new GetBankAccountsRequestValidator()
            .Validate(new GetBankAccountsRequest { Keyword = tooLong }).IsValid);
    }

    // ============================== 文本长度（创建与编辑同源）==============================

    [Fact]
    public void 编码名称开户行账号长度_创建与编辑应一致_边界值通过越界拒绝()
    {
        var codeOk = new string('c', BankAccountFieldConstraints.CodeMaxLength);
        var nameOk = new string('名', BankAccountFieldConstraints.NameMaxLength);
        var bankNameOk = new string('银', BankAccountFieldConstraints.BankNameMaxLength);
        var accountNoOk = new string('1', BankAccountFieldConstraints.AccountNoMaxLength);

        Assert.True(ValidateCreate(codeOk, nameOk, bankNameOk, accountNoOk));
        Assert.True(ValidateUpdate(codeOk, nameOk, bankNameOk, accountNoOk));

        Assert.False(ValidateCreate($"{codeOk}c", nameOk, bankNameOk, accountNoOk));
        Assert.False(ValidateUpdate(codeOk, $"{nameOk}名", bankNameOk, accountNoOk));
        Assert.False(ValidateCreate(codeOk, nameOk, $"{bankNameOk}银", accountNoOk));
        Assert.False(ValidateUpdate(codeOk, nameOk, bankNameOk, $"{accountNoOk}1"));

        // 必填：编码 / 名称为空拒绝
        Assert.False(ValidateCreate(string.Empty, nameOk, bankNameOk, accountNoOk));
        Assert.False(ValidateUpdate(codeOk, " ", bankNameOk, accountNoOk));
    }

    [Fact]
    public void 初始余额区间与小数位_创建与编辑应一致()
    {
        Assert.True(ValidateBalance(BankAccountFieldConstraints.InitialBalanceMin));
        Assert.False(ValidateBalance(BankAccountFieldConstraints.InitialBalanceMin - 0.01m));
        // 与 numeric(18,2) 一致：超过两位小数拒绝
        Assert.False(ValidateBalance(1000.001m));
    }

    [Fact]
    public void 账户类型与状态非法取值_应校验不通过()
    {
        var request = new CreateBankAccountRequest
        {
            Code = "BANK001",
            Name = "基本户",
            Type = 9,
            BankName = "招商银行",
            InitialBalance = 0m,
        };
        Assert.False(new CreateBankAccountRequestValidator().Validate(request).IsValid);

        Assert.False(new UpdateBankAccountRequestValidator().Validate(new UpdateBankAccountRequest
        {
            Id = Guid.NewGuid(),
            Code = "BANK001",
            Name = "基本户",
            Type = (int)BankAccountType.Bank,
            BankName = "招商银行",
            InitialBalance = 0m,
            Status = 9,
        }).IsValid);
    }

    // ============================== 预置现金账户种子 ==============================

    [Fact]
    public async Task 预置现金账户种子_应幂等且为现金类型()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        using var services = CreateInitializerServices(dbContext);

        await DatabaseInitializer.InitializeAsync(services, applyMigrations: false);
        var cash = Assert.Single(await dbContext.BankAccounts.ToListAsync());

        Assert.Equal(DatabaseInitializer.PresetCashAccountCode, cash.Code);
        Assert.Equal(BankAccountType.Cash, cash.Type);
        Assert.Equal(BankAccountStatus.Enabled, cash.Status);
        Assert.Equal(0m, cash.InitialBalance);
        Assert.Null(cash.BankName);
        Assert.Null(cash.AccountNo);

        // 幂等：重复执行不重复写入
        await DatabaseInitializer.InitializeAsync(services, applyMigrations: false);
        Assert.Equal(1, await dbContext.BankAccounts.CountAsync());
    }

    private static bool ValidateCreate(string code, string name, string bankName, string accountNo)
        => new CreateBankAccountRequestValidator().Validate(new CreateBankAccountRequest
        {
            Code = code,
            Name = name,
            Type = (int)BankAccountType.Bank,
            BankName = bankName,
            AccountNo = accountNo,
            InitialBalance = 0m,
        }).IsValid;

    private static bool ValidateUpdate(string code, string name, string bankName, string accountNo)
        => new UpdateBankAccountRequestValidator().Validate(new UpdateBankAccountRequest
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Type = (int)BankAccountType.Bank,
            BankName = bankName,
            AccountNo = accountNo,
            InitialBalance = 0m,
        }).IsValid;

    private static bool ValidateBalance(decimal initialBalance)
        => ValidateCreateBalance(initialBalance) && ValidateUpdateBalance(initialBalance);

    private static bool ValidateCreateBalance(decimal initialBalance)
        => new CreateBankAccountRequestValidator().Validate(new CreateBankAccountRequest
        {
            Code = "BANK001",
            Name = "基本户",
            Type = (int)BankAccountType.Bank,
            BankName = "招商银行",
            InitialBalance = initialBalance,
        }).IsValid;

    private static bool ValidateUpdateBalance(decimal initialBalance)
        => new UpdateBankAccountRequestValidator().Validate(new UpdateBankAccountRequest
        {
            Id = Guid.NewGuid(),
            Code = "BANK001",
            Name = "基本户",
            Type = (int)BankAccountType.Bank,
            BankName = "招商银行",
            InitialBalance = initialBalance,
        }).IsValid;

    private static ServiceProvider CreateInitializerServices(AppDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton(TestSupport.PasswordHasher);
        return services.BuildServiceProvider();
    }
}
