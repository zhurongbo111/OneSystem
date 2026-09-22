using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Vouchers.CreateVoucher;
using App.Core.Features.Vouchers.GetVoucherById;
using App.Core.Features.Vouchers.VoidVoucher;
using App.Core.Finance;

namespace App.Tests;

/// <summary>
/// 手工凭证相关用例测试（specs/033-erp-general-ledger tasks 6.2）：
/// 平衡通过 / 不平衡 40155 / 无分录 40156 / 科目非法 40157 / 期间已结账 40154 / 期间不存在 40159；
/// 详情 40400；作废（成功 / 已作废 40104 / 期间已结账 40154 / 40400）
/// </summary>
public class VoucherRequestHandlerTests
{
    private static readonly DateTimeOffset VoucherDate = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static CreateVoucherRequestHandler CreateHandler(
        GeneralLedgerStubs gl,
        RecordingUnitOfWork uow,
        StubCurrentUser user,
        Core.Abstractions.IAuditLogger audit)
        => new(gl.Vouchers, gl.Periods, gl.Accounts, uow, user, audit);

    private static CreateVoucherRequest Request(
        GeneralLedgerStubs gl,
        decimal debit = 100m,
        decimal credit = 100m,
        DateTimeOffset? voucherDate = null,
        Guid? accountId = null)
    {
        var account = accountId ?? gl.AccountsByKey[AccountMappingKeys.Inventory].Id;
        var counterAccount = gl.AccountsByKey[AccountMappingKeys.Payable].Id;
        return new CreateVoucherRequest
        {
            VoucherDate = voucherDate ?? VoucherDate,
            Summary = "手工调整",
            Items =
            [
                new CreateVoucherItem { AccountId = account, Debit = debit, Credit = 0m },
                new CreateVoucherItem { AccountId = counterAccount, Debit = 0m, Credit = credit },
            ],
        };
    }

    [Fact]
    public async Task 录入手工凭证_平衡_应落库并写审计()
    {
        var gl = GeneralLedgerStubs.Create();
        var uow = new RecordingUnitOfWork();
        var audit = new RecordingAuditLogger();
        var handler = CreateHandler(gl, uow, new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(Request(gl));

        Assert.Equal("记-202609-0001", result.VoucherNo);
        Assert.Equal(100m, result.TotalDebit);
        Assert.Equal(100m, result.TotalCredit);
        Assert.Equal((int)VoucherStatus.Posted, result.Status);
        Assert.Equal((int)VoucherSourceType.Manual, result.SourceType);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Items[0].LineNo);
        Assert.Equal(2, result.Items[1].LineNo);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Voucher, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Equal("记-202609-0001", entry.ResourceNo);
    }

    [Fact]
    public async Task 录入手工凭证_不平衡_应报VoucherUnbalanced()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(Request(gl, debit: 100m, credit: 99m)));

        Assert.Equal(ErrorCode.VoucherUnbalanced, ex.Code);
        Assert.Empty(gl.Vouchers.Appended);
    }

    [Fact]
    public async Task 录入手工凭证_无分录_应报VoucherNoEntries()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateVoucherRequest
        {
            VoucherDate = VoucherDate,
            Summary = "空凭证",
            Items = [],
        }));

        Assert.Equal(ErrorCode.VoucherNoEntries, ex.Code);
    }

    [Fact]
    public async Task 录入手工凭证_科目不存在_应报VoucherAccountInvalid()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(Request(gl, accountId: Guid.NewGuid())));

        Assert.Equal(ErrorCode.VoucherAccountInvalid, ex.Code);
    }

    [Fact]
    public async Task 录入手工凭证_科目非末级_应报VoucherAccountInvalid()
    {
        var gl = GeneralLedgerStubs.Create();
        var parent = gl.AccountsByKey[AccountMappingKeys.Inventory];
        // 给上级科目挂一个子科目 → 上级不再是末级科目
        gl.Accounts.Items.Add(new Account
        {
            Id = Guid.NewGuid(),
            Code = "140501",
            Name = "库存商品-明细",
            Category = AccountCategory.Asset,
            Direction = AccountDirection.Debit,
            ParentId = parent.Id,
            Status = AccountStatus.Enabled,
        });

        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(Request(gl, accountId: parent.Id)));

        Assert.Equal(ErrorCode.VoucherAccountInvalid, ex.Code);
        Assert.Contains("非末级", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 录入手工凭证_科目已停用_应报VoucherAccountInvalid()
    {
        var gl = GeneralLedgerStubs.Create();
        gl.AccountsByKey[AccountMappingKeys.Inventory].Status = AccountStatus.Disabled;
        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(Request(gl)));

        Assert.Equal(ErrorCode.VoucherAccountInvalid, ex.Code);
        Assert.Contains("已停用", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 录入手工凭证_期间已结账_应报PeriodClosed()
    {
        var gl = GeneralLedgerStubs.Create();
        gl.Periods.Seed(2026, 9, PeriodStatus.Closed);
        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(Request(gl)));

        Assert.Equal(ErrorCode.PeriodClosed, ex.Code);
    }

    [Fact]
    public async Task 录入手工凭证_期间不存在_应报PeriodNotOpened()
    {
        var gl = GeneralLedgerStubs.Create();
        gl.Periods.AutoCreate = false;
        var handler = CreateHandler(gl, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(Request(gl)));

        Assert.Equal(ErrorCode.PeriodNotOpened, ex.Code);
    }

    [Fact]
    public async Task 凭证详情_不存在_应报NotFound()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = new GetVoucherByIdRequestHandler(gl.Vouchers);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetVoucherByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废凭证_成功_应置作废并写审计()
    {
        var gl = GeneralLedgerStubs.Create();
        var user = new StubCurrentUser(Guid.NewGuid());
        var audit = new RecordingAuditLogger();
        var created = await CreateHandler(gl, new RecordingUnitOfWork(), user, audit).HandleAsync(Request(gl));

        var voidHandler = new VoidVoucherRequestHandler(gl.Vouchers, gl.Periods, new RecordingUnitOfWork(), user, audit);
        var result = await voidHandler.HandleAsync(new VoidVoucherRequest { Id = Guid.Parse(created.Id) });

        Assert.Equal((int)VoucherStatus.Voided, result.Status);
        Assert.Equal("记-202609-0001", result.VoucherNo);
        Assert.Equal(AuditAction.Void, audit.Entries[^1].Action);
        Assert.Equal(AuditResource.Voucher, audit.Entries[^1].Resource);
    }

    [Fact]
    public async Task 作废凭证_已作废_应报OrderVoided()
    {
        var gl = GeneralLedgerStubs.Create();
        var user = new StubCurrentUser(Guid.NewGuid());
        var created = await CreateHandler(gl, new RecordingUnitOfWork(), user, TestSupport.AuditLogger).HandleAsync(Request(gl));

        var voidHandler = new VoidVoucherRequestHandler(gl.Vouchers, gl.Periods, new RecordingUnitOfWork(), user, TestSupport.AuditLogger);
        await voidHandler.HandleAsync(new VoidVoucherRequest { Id = Guid.Parse(created.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => voidHandler.HandleAsync(new VoidVoucherRequest { Id = Guid.Parse(created.Id) }));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 作废凭证_期间已结账_应报PeriodClosed()
    {
        var gl = GeneralLedgerStubs.Create();
        var user = new StubCurrentUser(Guid.NewGuid());
        var created = await CreateHandler(gl, new RecordingUnitOfWork(), user, TestSupport.AuditLogger).HandleAsync(Request(gl));

        var period = gl.Periods.Seed(2026, 9, PeriodStatus.Open);
        period.Status = PeriodStatus.Closed;

        var voidHandler = new VoidVoucherRequestHandler(gl.Vouchers, gl.Periods, new RecordingUnitOfWork(), user, TestSupport.AuditLogger);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => voidHandler.HandleAsync(new VoidVoucherRequest { Id = Guid.Parse(created.Id) }));

        Assert.Equal(ErrorCode.PeriodClosed, ex.Code);
    }

    [Fact]
    public async Task 作废凭证_不存在_应报NotFound()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = new VoidVoucherRequestHandler(gl.Vouchers, gl.Periods, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidVoucherRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
