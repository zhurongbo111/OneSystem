using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.BankAccounts.CreateBankAccount;
using App.Core.Features.BankAccounts.DeleteBankAccount;
using App.Core.Features.BankAccounts.GetBankAccountById;
using App.Core.Features.BankAccounts.GetBankAccountSummary;
using App.Core.Features.BankAccounts.GetBankAccounts;
using App.Core.Features.BankAccounts.UpdateBankAccount;
using App.Core.Features.BankAccounts.UpdateBankAccountStatus;
using App.Core.Features.CashJournals.GetCashJournal;
using App.Core.Features.Settlements.CreateSettlement;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 资金出纳用例测试（specs/034-erp-cash design.md §6）：
/// 资金账户（新增 / 编辑 / 启停 / 删除 / 列表 / 余额总览）、资金日记账（期初 / 逐笔结余 / 期末 / 40400）、
/// 收付款单（`023`）增挂资金账户后的结算方式与账户类型匹配校验。
/// </summary>
public class CashRequestHandlerTests
{
    private static readonly DateTimeOffset SettlementDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset JournalStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset JournalEnd = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    private static BankAccount NewBankAccount(
        string code = "BANK001",
        BankAccountType type = BankAccountType.Bank,
        BankAccountStatus status = BankAccountStatus.Enabled,
        string? bankName = "招商银行深圳分行")
    {
        var now = DateTimeOffset.UtcNow;
        return new BankAccount
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = "基本户",
            Type = type,
            BankName = type == BankAccountType.Bank ? bankName : null,
            AccountNo = type == BankAccountType.Bank ? "755912345678901" : null,
            InitialBalance = 1000m,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private sealed record Harness(
        FakeBankAccountRepository Repository,
        RecordingUnitOfWork Uow,
        StubCurrentUser User,
        List<string> Calls);

    private static Harness NewContext()
    {
        var calls = new List<string>();
        return new Harness(
            new FakeBankAccountRepository(),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(Guid.NewGuid()),
            calls);
    }

    private static CreateBankAccountRequest CreateRequest(
        string code = "BANK001",
        int type = (int)BankAccountType.Bank,
        string? bankName = "招商银行深圳分行",
        string? accountNo = "755912345678901",
        decimal initialBalance = 1000m)
        => new()
        {
            Code = code,
            Name = "基本户",
            Type = type,
            BankName = bankName,
            AccountNo = accountNo,
            InitialBalance = initialBalance,
        };

    // ============================== 新增资金账户 ==============================

    [Fact]
    public async Task 新增资金账户_成功_应返回详情并写入初始余额()
    {
        var h = NewContext();

        var result = await new CreateBankAccountRequestHandler(h.Repository, h.Uow, h.User, TestSupport.AuditLogger)
            .HandleAsync(CreateRequest());

        Assert.Equal("BANK001", result.Code);
        Assert.Equal((int)BankAccountType.Bank, result.Type);
        Assert.Equal(1000m, result.InitialBalance);
        Assert.Equal((int)BankAccountStatus.Enabled, result.Status);
        // 事务已提交：业务写与审计日志同成功
        Assert.Equal(["Begin", "Commit"], h.Calls);
    }

    [Fact]
    public async Task 新增现金账户_应忽略开户行与银行账号()
    {
        var h = NewContext();

        var result = await new CreateBankAccountRequestHandler(h.Repository, h.Uow, h.User, TestSupport.AuditLogger)
            .HandleAsync(CreateRequest(code: "CASHBOX", type: (int)BankAccountType.Cash, bankName: "不应入库", accountNo: "不应入库"));

        Assert.Equal((int)BankAccountType.Cash, result.Type);
        Assert.Null(result.BankName);
        Assert.Null(result.AccountNo);
    }

    [Fact]
    public async Task 新增资金账户_编码重复_应返回40160()
    {
        var h = NewContext();
        h.Repository.Seed(NewBankAccount("BANK001"));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new CreateBankAccountRequestHandler(h.Repository, h.Uow, h.User, TestSupport.AuditLogger)
                .HandleAsync(CreateRequest("bank001")));

        Assert.Equal(ErrorCode.BankAccountCodeExists, ex.Code);
        // 校验失败在业务写之前：不开启事务
        Assert.DoesNotContain("Begin", h.Calls);
    }

    [Fact]
    public void 新增银行账户_缺开户行_应校验不通过()
    {
        var validator = new CreateBankAccountRequestValidator();

        var ex = new CreateBankAccountRequestValidator()
            .Validate(CreateRequest(type: (int)BankAccountType.Bank, bankName: " "));
        Assert.False(ex.IsValid);

        // 现金账户不要求开户行
        Assert.True(validator.Validate(CreateRequest(type: (int)BankAccountType.Cash, bankName: null)).IsValid);
    }

    [Fact]
    public void 新增资金账户_初始余额为负或小数位超限_应校验不通过()
    {
        Assert.False(new CreateBankAccountRequestValidator().Validate(CreateRequest(initialBalance: -0.01m)).IsValid);
        Assert.False(new CreateBankAccountRequestValidator().Validate(CreateRequest(initialBalance: 1000.001m)).IsValid);
        Assert.True(new CreateBankAccountRequestValidator().Validate(CreateRequest(initialBalance: 0m)).IsValid);
    }

    // ============================== 详情 / 编辑 / 启停 / 删除 ==============================

    [Fact]
    public async Task 查询资金账户详情_不存在_应返回40400()
    {
        var h = NewContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new GetBankAccountByIdRequestHandler(h.Repository)
                .HandleAsync(new GetBankAccountByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 编辑资金账户_应全量覆盖字段()
    {
        var h = NewContext();
        var account = NewBankAccount();
        h.Repository.Seed(account);

        var result = await new UpdateBankAccountRequestHandler(h.Repository, h.Uow, h.User, TestSupport.AuditLogger)
            .HandleAsync(new UpdateBankAccountRequest
            {
                Id = account.Id,
                Code = "BANK002",
                Name = "一般户",
                Type = (int)BankAccountType.Cash,
                BankName = "随类型切换清空",
                AccountNo = "随类型切换清空",
                InitialBalance = 200m,
                Status = (int)BankAccountStatus.Disabled,
                Remark = null,
            });

        Assert.Equal("BANK002", result.Code);
        Assert.Equal("一般户", result.Name);
        Assert.Equal((int)BankAccountType.Cash, result.Type);
        // 类型切为现金后，开户行 / 账号被清空（不再是脏快照）
        Assert.Null(result.BankName);
        Assert.Null(result.AccountNo);
        Assert.Equal(200m, result.InitialBalance);
        Assert.Equal((int)BankAccountStatus.Disabled, result.Status);
        Assert.Equal(["Begin", "Commit"], h.Calls);
    }

    [Fact]
    public async Task 编辑资金账户_编码被其他账户占用_应返回40160()
    {
        var h = NewContext();
        var target = NewBankAccount("BANK001");
        h.Repository.Seed(target);
        h.Repository.Seed(NewBankAccount("BANK009"));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new UpdateBankAccountRequestHandler(h.Repository, h.Uow, h.User, TestSupport.AuditLogger)
                .HandleAsync(new UpdateBankAccountRequest
                {
                    Id = target.Id,
                    Code = "BANK009",
                    Name = "基本户",
                    Type = (int)BankAccountType.Bank,
                    BankName = "招商银行深圳分行",
                    InitialBalance = 0m,
                }));

        Assert.Equal(ErrorCode.BankAccountCodeExists, ex.Code);
        // 校验失败在业务写之前：不开启事务
        Assert.DoesNotContain("Begin", h.Calls);
    }

    [Fact]
    public async Task 启停资金账户_应更新状态()
    {
        var h = NewContext();
        var account = NewBankAccount();
        h.Repository.Seed(account);

        var result = await new UpdateBankAccountStatusRequestHandler(h.Repository, h.Uow, h.User, TestSupport.AuditLogger)
            .HandleAsync(new UpdateBankAccountStatusRequest { Id = account.Id, Status = (int)BankAccountStatus.Disabled });

        Assert.Equal((int)BankAccountStatus.Disabled, result.Status);
        Assert.Equal(BankAccountStatus.Disabled, h.Repository.Get(account.Id).Status);
    }

    [Fact]
    public async Task 删除资金账户_未被引用_应删除成功()
    {
        var h = NewContext();
        var account = NewBankAccount();
        h.Repository.Seed(account);

        await new DeleteBankAccountRequestHandler(h.Repository, h.Uow, TestSupport.AuditLogger)
            .HandleAsync(new DeleteBankAccountRequest { Id = account.Id });

        Assert.Null(await h.Repository.GetByIdAsync(account.Id));
        Assert.Equal(["Begin", "Commit"], h.Calls);
    }

    [Fact]
    public async Task 删除资金账户_已被收付款单引用_应返回40161且保留账户()
    {
        var h = NewContext();
        var account = NewBankAccount();
        h.Repository.Seed(account);
        h.Repository.Referenced = true;

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new DeleteBankAccountRequestHandler(h.Repository, h.Uow, TestSupport.AuditLogger)
                .HandleAsync(new DeleteBankAccountRequest { Id = account.Id }));

        Assert.Equal(ErrorCode.BankAccountInUse, ex.Code);
        Assert.NotNull(await h.Repository.GetByIdAsync(account.Id));
    }

    // ============================== 列表 / 余额总览 ==============================

    [Fact]
    public async Task 查询资金账户列表_应透传筛选并分页映射()
    {
        var h = NewContext();
        var account = NewBankAccount();
        h.Repository.PagedItems =
        [
            new BankAccountListItem
            {
                Id = account.Id, Code = account.Code, Name = account.Name, Type = account.Type,
                BankName = account.BankName, AccountNo = account.AccountNo, InitialBalance = 1000m,
                Status = account.Status, Remark = null, Balance = 1200m,
                CreatedAt = account.CreatedAt, UpdatedAt = account.UpdatedAt,
            },
        ];
        h.Repository.PagedTotal = 3;

        var result = await new GetBankAccountsRequestHandler(h.Repository).HandleAsync(new GetBankAccountsRequest
        {
            Keyword = "基本",
            Type = (int)BankAccountType.Bank,
            Status = (int)BankAccountStatus.Enabled,
            Page = 2,
            PageSize = 10,
        });

        var query = Assert.Single(h.Repository.PagedQueries);
        Assert.Equal("基本", query.Keyword);
        Assert.Equal(BankAccountType.Bank, query.Type);
        Assert.Equal(BankAccountStatus.Enabled, query.Status);
        Assert.Equal(2, query.Page);

        Assert.Equal(3, result.Total);
        var row = Assert.Single(result.Items);
        Assert.Equal(account.Id.ToString(), row.Id);
        Assert.Equal(1000m, row.InitialBalance);
        // 派生列：初始余额 + Σ 收 − Σ 付（由收付款单聚合，不落列）
        Assert.Equal(1200m, row.Balance);
    }

    [Fact]
    public async Task 余额总览_应返回各账户派生余额()
    {
        var h = NewContext();
        var account = NewBankAccount();
        h.Repository.Balances =
        [
            new BankAccountBalanceItem
            {
                Id = account.Id, Code = account.Code, Name = account.Name,
                Type = account.Type, Status = account.Status, Balance = 1200m,
            },
        ];

        var result = await new GetBankAccountSummaryRequestHandler(h.Repository)
            .HandleAsync(new GetBankAccountSummaryRequest());

        var row = Assert.Single(result);
        Assert.Equal(account.Id.ToString(), row.Id);
        Assert.Equal((int)BankAccountType.Bank, row.Type);
        Assert.Equal(1200m, row.Balance);
    }

    // ============================== 资金日记账 ==============================

    [Fact]
    public async Task 查询资金日记账_应返回期初逐笔结余与期末()
    {
        var repository = new FakeCashJournalQueryRepository
        {
            OpeningBalance = 1000m,
            Entries =
            [
                new CashJournalEntryItem { Date = SettlementDate, SettlementNo = "RC202601010001", Summary = "收款-客户一", Debit = 500m, Credit = 0m },
                new CashJournalEntryItem { Date = SettlementDate, SettlementNo = "PY202601010001", Summary = "付款-供应商一", Debit = 0m, Credit = 300m },
            ],
        };

        var result = await new GetCashJournalRequestHandler(repository).HandleAsync(new GetCashJournalRequest
        {
            BankAccountId = Guid.NewGuid(),
            Start = JournalStart,
            End = JournalEnd,
        });

        var query = Assert.Single(repository.JournalQueries);
        Assert.Equal(JournalStart, query.Start);
        Assert.Equal(JournalEnd, query.End);

        Assert.Equal(1000m, result.OpeningBalance);
        Assert.Equal(1200m, result.ClosingBalance);
        Assert.Equal([1500m, 1200m], result.Entries.Select(e => e.Balance));
        Assert.Equal([500m, 0m], result.Entries.Select(e => e.Debit));
        Assert.Equal([0m, 300m], result.Entries.Select(e => e.Credit));
    }

    [Fact]
    public async Task 查询资金日记账_区间无流水_期末应等于期初()
    {
        var repository = new FakeCashJournalQueryRepository { OpeningBalance = 800m };

        var result = await new GetCashJournalRequestHandler(repository).HandleAsync(new GetCashJournalRequest
        {
            BankAccountId = Guid.NewGuid(),
            Start = JournalStart,
            End = JournalEnd,
        });

        Assert.Empty(result.Entries);
        Assert.Equal(800m, result.OpeningBalance);
        Assert.Equal(800m, result.ClosingBalance);
    }

    [Fact]
    public async Task 查询资金日记账_账户不存在_应返回40400()
    {
        var repository = new FakeCashJournalQueryRepository { Exists = false };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new GetCashJournalRequestHandler(repository).HandleAsync(new GetCashJournalRequest
            {
                BankAccountId = Guid.NewGuid(),
                Start = JournalStart,
                End = JournalEnd,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public void 查询资金日记账_结束日期早于起始日期_应校验不通过()
    {
        var validator = new GetCashJournalRequestValidator();

        Assert.False(validator.Validate(new GetCashJournalRequest
        {
            BankAccountId = Guid.NewGuid(),
            Start = JournalEnd,
            End = JournalStart,
        }).IsValid);

        // 同一天为合法闭区间
        Assert.True(validator.Validate(new GetCashJournalRequest
        {
            BankAccountId = Guid.NewGuid(),
            Start = JournalStart,
            End = JournalStart,
        }).IsValid);
    }

    // ============================== 收付款单 → 资金账户类型匹配 ==============================

    [Fact]
    public async Task 新增收款单_现金结算关联银行账户_应返回40162()
    {
        var harness = await NewSettlementHarnessAsync();
        var order = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(order, Array.Empty<SalesShipmentItem>());
        var bank = NewBankAccount();
        harness.BankAccounts.Seed(bank);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSettlementHandler(harness).HandleAsync(
            SettlementRequest(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash, bank.Id,
                new CreateSettlementItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = order.Id, Amount = 100m })));

        Assert.Equal(ErrorCode.BankAccountTypeMismatch, ex.Code);
        // 资金账户校验在核销明细落库之前：不开启事务
        Assert.DoesNotContain("Begin", harness.Calls);
    }

    [Fact]
    public async Task 新增收款单_银行转账关联现金账户_应返回40162()
    {
        var harness = await NewSettlementHarnessAsync();
        var order = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(order, Array.Empty<SalesShipmentItem>());
        var cash = NewBankAccount("CASH", BankAccountType.Cash, bankName: null);
        harness.BankAccounts.Seed(cash);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSettlementHandler(harness).HandleAsync(
            SettlementRequest(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.BankTransfer, cash.Id,
                new CreateSettlementItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = order.Id, Amount = 100m })));

        Assert.Equal(ErrorCode.BankAccountTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task 新增收款单_账户不存在_应返回40400()
    {
        var harness = await NewSettlementHarnessAsync();
        var order = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(order, Array.Empty<SalesShipmentItem>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSettlementHandler(harness).HandleAsync(
            SettlementRequest(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.BankTransfer, Guid.NewGuid(),
                new CreateSettlementItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = order.Id, Amount = 100m })));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增收款单_银行转账关联银行账户_应落库资金账户()
    {
        var harness = await NewSettlementHarnessAsync();
        var order = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(order, Array.Empty<SalesShipmentItem>());
        var bank = NewBankAccount();
        harness.BankAccounts.Seed(bank);
        harness.Settlements.BankAccountNames[bank.Id] = bank.Name;

        var result = await CreateSettlementHandler(harness).HandleAsync(
            SettlementRequest(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.BankTransfer, bank.Id,
                new CreateSettlementItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = order.Id, Amount = 100m }));

        Assert.Equal(bank.Id, result.BankAccountId);
        Assert.Equal(bank.Name, result.BankAccountName);
    }

    [Fact]
    public async Task 新增收款单_其他结算方式_不关联账户应通过()
    {
        var harness = await NewSettlementHarnessAsync();
        var order = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(order, Array.Empty<SalesShipmentItem>());

        var result = await CreateSettlementHandler(harness).HandleAsync(
            SettlementRequest(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Other, null,
                new CreateSettlementItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = order.Id, Amount = 100m }));

        Assert.Null(result.BankAccountId);
        Assert.Null(result.BankAccountName);
    }

    [Fact]
    public async Task 新增收款单_关联停用账户_应返回参数错误()
    {
        var harness = await NewSettlementHarnessAsync();
        var order = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(order, Array.Empty<SalesShipmentItem>());
        var disabled = NewBankAccount(status: BankAccountStatus.Disabled);
        harness.BankAccounts.Seed(disabled);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateSettlementHandler(harness).HandleAsync(
            SettlementRequest(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.BankTransfer, disabled.Id,
                new CreateSettlementItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = order.Id, Amount = 100m })));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    // ============================== 测试支撑 ==============================

    private sealed record SettlementHarness(
        AppDbContext Context,
        Partner Partner,
        FakeSettlementRepository Settlements,
        FakePurchaseReceiptRepository PurchaseReceipts,
        FakeSalesShipmentRepository SalesShipments,
        FakePurchaseReturnRepository PurchaseReturns,
        FakeSalesReturnRepository SalesReturns,
        FakeBankAccountRepository BankAccounts,
        RecordingUnitOfWork Uow,
        StubCurrentUser User,
        List<string> Calls);

    private static async Task<SettlementHarness> NewSettlementHarnessAsync()
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("往来一", PartnerType.Customer, PartnerStatus.Enabled);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var calls = new List<string>();
        return new SettlementHarness(
            context,
            partner,
            new FakeSettlementRepository(),
            new FakePurchaseReceiptRepository(),
            new FakeSalesShipmentRepository(),
            new FakePurchaseReturnRepository(),
            new FakeSalesReturnRepository(),
            new FakeBankAccountRepository(),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(Guid.NewGuid()),
            calls);
    }

    private static CreateSettlementRequestHandler CreateSettlementHandler(SettlementHarness harness)
    {
        var gl = GeneralLedgerStubs.Create();
        return new(
            harness.Settlements,
            harness.PurchaseReceipts,
            harness.SalesShipments,
            harness.PurchaseReturns,
            harness.SalesReturns,
            new PartnerRepository(harness.Context),
            harness.BankAccounts,
            gl.Vouchers,
            gl.Mappings,
            gl.Periods,
            gl.Accounts,
            harness.Uow,
            harness.User,
            TestSupport.AuditLogger);
    }

    private static CreateSettlementRequest SettlementRequest(
        Guid partnerId,
        SettlementType type,
        SettlementMethod method,
        Guid? bankAccountId,
        params CreateSettlementItem[] items)
        => new()
        {
            Type = type,
            PartnerId = partnerId,
            SettlementDate = SettlementDate,
            Method = method,
            BankAccountId = bankAccountId,
            Items = items,
        };

    private static SalesShipment NewSalesShipment(Guid partnerId, decimal totalAmount = 1000m)
    {
        var now = DateTimeOffset.UtcNow;
        return new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = "GI202512200001",
            PartnerId = partnerId,
            PartnerName = "往来一",
            OrderDate = new DateTimeOffset(2025, 12, 20, 0, 0, 0, TimeSpan.Zero),
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
