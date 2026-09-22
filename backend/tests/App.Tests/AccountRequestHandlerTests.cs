using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Accounts.CreateAccount;
using App.Core.Features.Accounts.DeleteAccount;
using App.Core.Features.Accounts.GetAccountById;
using App.Core.Features.Accounts.GetAccounts;
using App.Core.Features.Accounts.UpdateAccount;
using App.Core.Features.Accounts.UpdateAccountStatus;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 会计科目用例处理器测试（树 / 新增 / 详情 / 编辑（防环）/ 删除保护 / 启停）
/// </summary>
public class AccountRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetAccountsRequestHandler CreateGetAccountsHandler(AppDbContext dbContext)
        => new(new AccountRepository(dbContext));

    private static CreateAccountRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new AccountRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdateAccountRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new AccountRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static DeleteAccountRequestHandler CreateDeleteHandler(AppDbContext dbContext, IAccountRepository? repository = null)
        => new(
            repository ?? new AccountRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.AuditLogger);

    private static UpdateAccountStatusRequestHandler CreateStatusHandler(AppDbContext dbContext)
        => new(
            new AccountRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    /// <summary>构建科目实体（默认启用、非预置、借方）</summary>
    private static Account NewAccount(
        string code = "1001",
        string name = "库存现金",
        Guid? parentId = null,
        AccountCategory category = AccountCategory.Asset,
        AccountDirection direction = AccountDirection.Debit,
        int sortOrder = 0,
        bool isPreset = false,
        AccountStatus status = AccountStatus.Enabled)
    {
        var now = DateTimeOffset.UtcNow;
        return new Account
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = category,
            Direction = direction,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsPreset = isPreset,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ============================== 树 ==============================

    [Fact]
    public async Task GetAccounts_多级科目_应按上级组装树并排序()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var cash = NewAccount("1001", "库存现金", sortOrder: 2);
        var bank = NewAccount("1002", "银行存款", sortOrder: 1);
        var bankChildA = NewAccount("100201", "工行", bank.Id, sortOrder: 2);
        var bankChildB = NewAccount("100202", "建行", bank.Id, sortOrder: 1);
        var grandChild = NewAccount("10020201", "建行基本户", bankChildB.Id);
        dbContext.Accounts.AddRange(cash, bank, bankChildA, bankChildB, grandChild);
        await dbContext.SaveChangesAsync();

        var tree = await CreateGetAccountsHandler(dbContext).HandleAsync(new GetAccountsRequest());

        // 一级节点按 SortOrder 升序：银行存款(1) → 库存现金(2)
        Assert.Equal(["银行存款", "库存现金"], tree.Select(n => n.Name).ToList());
        var bankNode = tree[0];
        Assert.Equal(["建行", "工行"], bankNode.Children.Select(n => n.Name).ToList());
        Assert.Equal("建行基本户", bankNode.Children[0].Children.Single().Name);
        Assert.Empty(bankNode.Children[1].Children);
        Assert.Equal((int)AccountCategory.Asset, bankNode.Category);
        Assert.Equal((int)AccountDirection.Debit, bankNode.Direction);
        Assert.False(bankNode.IsPreset);
    }

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreateAccount_合法请求_应落库并写审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var parent = NewAccount();
        dbContext.Accounts.Add(parent);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreateAccountRequest
        {
            Code = " 100101 ",
            Name = " 人民币户 ",
            Category = (int)AccountCategory.Asset,
            Direction = (int)AccountDirection.Debit,
            ParentId = parent.Id,
            SortOrder = 5,
            Status = (int)AccountStatus.Enabled,
            Remark = "  基本户  ",
        });

        var saved = await dbContext.Accounts.SingleAsync(a => a.Id != parent.Id);
        Assert.Equal("100101", saved.Code);
        Assert.Equal("人民币户", saved.Name);
        Assert.Equal(parent.Id, saved.ParentId);
        Assert.Equal(5, saved.SortOrder);
        Assert.False(saved.IsPreset);
        Assert.Equal("基本户", saved.Remark);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal(saved.Id.ToString(), result.Id);
        Assert.Equal((int)AccountCategory.Asset, result.Category);
    }

    [Fact]
    public async Task CreateAccount_编码重复_应抛业务异常40149()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Accounts.Add(NewAccount("1001", "库存现金"));
        await dbContext.SaveChangesAsync();

        // 大小写不敏感：账套内 1001 与 1001 视为重复
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateAccountRequest { Code = "1001", Name = "重复科目" }));

        Assert.Equal(ErrorCode.AccountCodeExists, ex.Code);
        Assert.Equal(1, await dbContext.Accounts.CountAsync());
    }

    [Fact]
    public async Task CreateAccount_上级不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateAccountRequest
            {
                Code = "100101",
                Name = "人民币户",
                ParentId = Guid.NewGuid(),
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Equal(0, await dbContext.Accounts.CountAsync());
    }

    // ============================== 详情 ==============================

    [Fact]
    public async Task GetAccountById_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetAccountByIdRequestHandler(new AccountRepository(dbContext))
                .HandleAsync(new GetAccountByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑（防环）==============================

    [Fact]
    public async Task UpdateAccount_改名与改上级_应更新且保持预置标记()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var liability = NewAccount("2202", "应付账款", category: AccountCategory.Liability, direction: AccountDirection.Credit);
        var child = NewAccount("100201", "工行", isPreset: true);
        dbContext.Accounts.AddRange(liability, child);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdateAccountRequest
        {
            Id = child.Id,
            Code = "100202",
            Name = "工行（改名）",
            Category = (int)AccountCategory.Asset,
            Direction = (int)AccountDirection.Debit,
            ParentId = null,
            SortOrder = 3,
            Status = (int)AccountStatus.Disabled,
            Remark = "备注",
        });

        var saved = await dbContext.Accounts.SingleAsync(a => a.Id == child.Id);
        Assert.Equal("100202", saved.Code);
        Assert.Equal("工行（改名）", saved.Name);
        Assert.Null(saved.ParentId);
        Assert.Equal(AccountStatus.Disabled, saved.Status);
        Assert.True(saved.IsPreset);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal("备注", result.Remark);
    }

    [Fact]
    public async Task UpdateAccount_上级设为自身_应抛业务异常40141()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var account = NewAccount();
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAccountRequest
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                Category = (int)account.Category,
                Direction = (int)account.Direction,
                ParentId = account.Id,
            }));

        Assert.Equal(ErrorCode.DepartmentCycle, ex.Code);
    }

    [Fact]
    public async Task UpdateAccount_上级设为自身下级_应抛业务异常40141()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewAccount("1001", "库存现金");
        var child = NewAccount("100101", "人民币户", root.Id);
        var grandChild = NewAccount("10010101", "基本户", child.Id);
        dbContext.Accounts.AddRange(root, child, grandChild);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAccountRequest
            {
                Id = root.Id,
                Code = root.Code,
                Name = root.Name,
                Category = (int)root.Category,
                Direction = (int)root.Direction,
                ParentId = grandChild.Id,
            }));

        Assert.Equal(ErrorCode.DepartmentCycle, ex.Code);
        Assert.Null((await dbContext.Accounts.SingleAsync(a => a.Id == root.Id)).ParentId);
    }

    [Fact]
    public async Task UpdateAccount_上级不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var account = NewAccount();
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAccountRequest
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                Category = (int)account.Category,
                Direction = (int)account.Direction,
                ParentId = Guid.NewGuid(),
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task UpdateAccount_编码与他科目重复_应抛业务异常40149()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var account = NewAccount("1001", "库存现金");
        dbContext.Accounts.Add(account);
        dbContext.Accounts.Add(NewAccount("1002", "银行存款"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAccountRequest
            {
                Id = account.Id,
                Code = "1002",
                Name = "库存现金",
                Category = (int)account.Category,
                Direction = (int)account.Direction,
            }));

        Assert.Equal(ErrorCode.AccountCodeExists, ex.Code);
    }

    [Fact]
    public async Task UpdateAccount_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAccountRequest
            {
                Id = Guid.NewGuid(),
                Code = "1001",
                Name = "库存现金",
                Category = (int)AccountCategory.Asset,
                Direction = (int)AccountDirection.Debit,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 删除保护 ==============================

    [Fact]
    public async Task DeleteAccount_存在子科目_应抛业务异常40150()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewAccount();
        dbContext.Accounts.Add(root);
        dbContext.Accounts.Add(NewAccount("100101", "子科目", root.Id));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteAccountRequest { Id = root.Id }));

        Assert.Equal(ErrorCode.AccountInUse, ex.Code);
        Assert.Equal(2, await dbContext.Accounts.CountAsync());
    }

    [Fact]
    public async Task DeleteAccount_预置科目_应抛业务异常40150()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var preset = NewAccount("1001", "库存现金", isPreset: true);
        dbContext.Accounts.Add(preset);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteAccountRequest { Id = preset.Id }));

        Assert.Equal(ErrorCode.AccountInUse, ex.Code);
        Assert.Equal(1, await dbContext.Accounts.CountAsync());
    }

    [Fact]
    public async Task DeleteAccount_被凭证引用_应抛业务异常40153()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var account = NewAccount();
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        // 真实实现当前恒返回 false（凭证表由 033 落地），故用桩把该判定置真以覆盖 40153 分支
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext, new VoucherReferencedAccountRepository(dbContext))
                .HandleAsync(new DeleteAccountRequest { Id = account.Id }));

        Assert.Equal(ErrorCode.AccountReferencedByVoucher, ex.Code);
        Assert.Equal(1, await dbContext.Accounts.CountAsync());
    }

    [Fact]
    public async Task DeleteAccount_非预置空叶子科目_应删除()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var account = NewAccount("100101", "人民币户");
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        await CreateDeleteHandler(dbContext).HandleAsync(new DeleteAccountRequest { Id = account.Id });

        Assert.Empty(await dbContext.Accounts.ToListAsync());
    }

    [Fact]
    public async Task DeleteAccount_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteAccountRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 启停 ==============================

    [Fact]
    public async Task UpdateAccountStatus_停用_应更新状态()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var account = NewAccount();
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdateAccountStatusRequest
        {
            Id = account.Id,
            Status = (int)AccountStatus.Disabled,
        });

        Assert.Equal((int)AccountStatus.Disabled, result.Status);
        Assert.Equal(AccountStatus.Disabled, (await dbContext.Accounts.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateAccountStatus_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateStatusHandler(dbContext).HandleAsync(new UpdateAccountStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)AccountStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    /// <summary>
    /// 科目仓储桩：仅把「是否被凭证引用」置为恒真（真实实现在 033 落地前恒为 false），
    /// 其余方法委托真实仓储，用于覆盖 40153 删除保护分支
    /// </summary>
    private sealed class VoucherReferencedAccountRepository : IAccountRepository
    {
        private readonly AccountRepository _inner;

        public VoucherReferencedAccountRepository(AppDbContext dbContext)
        {
            _inner = new AccountRepository(dbContext);
        }

        public Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
            => _inner.GetAllAsync(cancellationToken);

        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => _inner.ExistsByCodeAsync(code, excludeId, cancellationToken);

        public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.HasChildrenAsync(id, cancellationToken);

        public Task<bool> IsReferencedByVoucherAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task AddAsync(Account account, CancellationToken cancellationToken = default)
            => _inner.AddAsync(account, cancellationToken);

        public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(account, cancellationToken);

        public Task DeleteAsync(Account account, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(account, cancellationToken);
    }
}