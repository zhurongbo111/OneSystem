using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Finance;

namespace App.Tests;

/// <summary>
/// 总账（`033`）相关依赖的测试桩组合：给「既有单据写入路径」的测试提供开箱可用的
/// 期间 / 科目映射 / 科目 / 凭证仓储，避免每个用例都要落真实数据。
/// 默认行为：期间自动按月创建且 <c>Open</c>、8 个映射键全部指向末级启用科目、凭证追加即成功。
/// </summary>
internal sealed class GeneralLedgerStubs
{
    /// <summary>凭证仓储桩（记录追加 / 作废的凭证，供断言）</summary>
    public StubVoucherRepository Vouchers { get; } = new();

    /// <summary>科目映射仓储桩</summary>
    public StubAccountMappingRepository Mappings { get; } = new();

    /// <summary>会计期间仓储桩</summary>
    public StubAccountingPeriodRepository Periods { get; } = new();

    /// <summary>会计科目仓储桩</summary>
    public StubAccountRepository Accounts { get; } = new();

    /// <summary>映射键 → 桩科目（断言科目快照用）</summary>
    public Dictionary<string, Account> AccountsByKey { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 新建凭证仓储桩（与期间桩互不依赖，可单独用于只需「作废凭证」的用例）
    /// </summary>
    public static StubVoucherRepository NewVouchers() => new();

    /// <summary>
    /// 新建期间仓储桩（默认自动创建未结账期间，保证既有单据用例不受总账改造影响）
    /// </summary>
    public static StubAccountingPeriodRepository NewPeriods() => new();

    /// <summary>
    /// 创建一套互相配套的桩（8 个映射键 → 末级启用科目）
    /// </summary>
    public static GeneralLedgerStubs Create()
    {
        var stubs = new GeneralLedgerStubs();
        foreach (var definition in AccountMappingKeys.All)
        {
            var (category, direction) = definition.Key switch
            {
                AccountMappingKeys.Payable => (AccountCategory.Liability, AccountDirection.Credit),
                AccountMappingKeys.Revenue or AccountMappingKeys.Cost => (AccountCategory.ProfitLoss, AccountDirection.Credit),
                AccountMappingKeys.Profit => (AccountCategory.Equity, AccountDirection.Credit),
                _ => (AccountCategory.Asset, AccountDirection.Debit),
            };

            var account = new Account
            {
                Id = Guid.NewGuid(),
                Code = definition.PresetAccountCode,
                Name = definition.Label,
                Category = category,
                Direction = direction,
                ParentId = null,
                IsPreset = true,
                Status = AccountStatus.Enabled,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            stubs.Accounts.Items.Add(account);
            stubs.AccountsByKey[definition.Key] = account;
            stubs.Mappings.Items[definition.Key] = account.Id;
        }

        return stubs;
    }
}

/// <summary>会计期间仓储桩：默认按月自动创建未结账期间；<see cref="AutoCreate"/> 置 false 可模拟「期间不存在」</summary>
internal sealed class StubAccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly Dictionary<(int Year, int Month), AccountingPeriod> _periods = [];

    /// <summary>查询不到期间时是否自动创建（默认 true；置 false 模拟 40159）</summary>
    public bool AutoCreate { get; set; } = true;

    /// <summary>
    /// 预置一个期间（指定状态），用于结账 / 已结账场景
    /// </summary>
    public AccountingPeriod Seed(int year, int month, PeriodStatus status = PeriodStatus.Open)
    {
        var period = new AccountingPeriod { Id = Guid.NewGuid(), Year = year, Month = month, Status = status };
        _periods[(year, month)] = period;
        return period;
    }

    /// <inheritdoc />
    public Task<AccountingPeriod?> GetByYearMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (_periods.TryGetValue((year, month), out var existing))
        {
            return Task.FromResult<AccountingPeriod?>(existing);
        }

        return Task.FromResult<AccountingPeriod?>(AutoCreate ? Seed(year, month) : null);
    }

    /// <inheritdoc />
    public Task<AccountingPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_periods.Values.FirstOrDefault(p => p.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<AccountingPeriod>> GetAllAsync(int? year = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AccountingPeriod>>(
            _periods.Values
                .Where(p => year is null || p.Year == year)
                .OrderBy(p => p.Year)
                .ThenBy(p => p.Month)
                .ToList());

    /// <inheritdoc />
    public Task SetStatusAsync(Guid id, PeriodStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var period = _periods.Values.FirstOrDefault(p => p.Id == id);
        if (period is not null)
        {
            period.Status = status;
            period.ClosedAt = status == PeriodStatus.Closed ? DateTimeOffset.UtcNow : null;
            period.ClosedBy = status == PeriodStatus.Closed ? operatorId : null;
        }

        return Task.CompletedTask;
    }
}

/// <summary>科目映射仓储桩：按键维护映射（默认由 <see cref="GeneralLedgerStubs.Create"/> 填满 8 个键）</summary>
internal sealed class StubAccountMappingRepository : IAccountMappingRepository
{
    /// <summary>映射键 → 科目 id</summary>
    public Dictionary<string, Guid> Items { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlyList<AccountMapping>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AccountMapping>>(
            Items.Select(kv => new AccountMapping
            {
                Id = Guid.NewGuid(),
                Key = kv.Key,
                AccountId = kv.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }).ToList());

    /// <inheritdoc />
    public Task<AccountMapping> UpsertAsync(string key, Guid accountId, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        Items[key] = accountId;
        return Task.FromResult(new AccountMapping
        {
            Id = Guid.NewGuid(),
            Key = key,
            AccountId = accountId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
    }
}

/// <summary>会计科目仓储桩：内存维护科目集合，只实现总账取数所需行为</summary>
internal sealed class StubAccountRepository : IAccountRepository
{
    /// <summary>科目集合</summary>
    public List<Account> Items { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Account>>(Items.OrderBy(a => a.Code).ToList());

    /// <inheritdoc />
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(a => string.Equals(a.Code, code.Trim(), StringComparison.OrdinalIgnoreCase) && a.Id != excludeId));

    /// <inheritdoc />
    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(a => a.ParentId == id));

    /// <inheritdoc />
    public Task<bool> IsReferencedByVoucherAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc />
    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        Items.Add(account);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task DeleteAsync(Account account, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(a => a.Id == account.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记账凭证仓储桩：记录追加的凭证与分录，按来源作废；供「自动凭证改造」断言（有凭证 / 已作废 / 借贷平衡）
/// </summary>
internal sealed class StubVoucherRepository : IVoucherRepository
{
    private readonly Dictionary<Guid, (Voucher Voucher, IReadOnlyList<VoucherEntry> Entries)> _vouchers = [];
    private int _sequence;

    /// <summary>按追加顺序记录的凭证与分录</summary>
    public List<(Voucher Voucher, IReadOnlyList<VoucherEntry> Entries)> Appended { get; } = [];

    /// <summary>按（来源类型 / 来源单据 id）记录的作废次数</summary>
    public List<(VoucherSourceType SourceType, Guid SourceId)> VoidedBySource { get; } = [];

    /// <summary>全部凭证（含已被作废的）</summary>
    public IReadOnlyCollection<Voucher> All => _vouchers.Values.Select(v => v.Voucher).ToList();

    /// <summary>按来源单据取凭证（断言用）</summary>
    public Voucher? FindBySource(Guid sourceId)
        => _vouchers.Values.Select(v => v.Voucher).FirstOrDefault(v => v.SourceId == sourceId);

    /// <inheritdoc />
    public Task AddAsync(Voucher voucher, IReadOnlyList<VoucherEntry> entries, CancellationToken cancellationToken = default)
    {
        _vouchers[voucher.Id] = (voucher, entries);
        Appended.Add((voucher, entries));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<Voucher> Items, int Total)> GetPagedAsync(
        int? year,
        int? month,
        VoucherSourceType? sourceType,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _vouchers.Values.Select(v => v.Voucher).AsEnumerable();
        if (year is not null && month is not null)
        {
            query = query.Where(v => v.VoucherDate.Year == year && v.VoucherDate.Month == month);
        }

        if (sourceType is not null)
        {
            query = query.Where(v => v.SourceType == sourceType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(v => v.VoucherNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || v.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.VoucherNo).ToList();
        return Task.FromResult<(IReadOnlyList<Voucher>, int)>(
            (list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), list.Count));
    }

    /// <inheritdoc />
    public Task<(Voucher? Voucher, IReadOnlyList<VoucherEntry> Entries)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_vouchers.TryGetValue(id, out var found)
            ? (found.Voucher, found.Entries)
            : (null, (IReadOnlyList<VoucherEntry>)Array.Empty<VoucherEntry>()));

    /// <inheritdoc />
    public Task<int> VoidAsync(Guid id, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        if (!_vouchers.TryGetValue(id, out var found) || found.Voucher.Status == VoucherStatus.Voided)
        {
            return Task.FromResult(0);
        }

        found.Voucher.Status = VoucherStatus.Voided;
        return Task.FromResult(1);
    }

    /// <inheritdoc />
    public Task<int> VoidBySourceAsync(VoucherSourceType sourceType, Guid sourceId, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var matched = _vouchers.Values
            .Select(v => v.Voucher)
            .Where(v => v.SourceType == sourceType && v.SourceId == sourceId && v.Status == VoucherStatus.Posted)
            .ToList();

        foreach (var voucher in matched)
        {
            voucher.Status = VoucherStatus.Voided;
        }

        VoidedBySource.Add((sourceType, sourceId));
        return Task.FromResult(matched.Count);
    }

    /// <inheritdoc />
    public Task<string> GenerateNoAsync(DateTimeOffset voucherDate, CancellationToken cancellationToken = default)
        => Task.FromResult($"{VoucherFieldConstraints.NoPrefix}{voucherDate:yyyyMM}-{++_sequence:D4}");
}
