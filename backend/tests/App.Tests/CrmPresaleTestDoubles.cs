using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型线索仓储假实现（内存存储 + 记录调用）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（043 design.md §6）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列（断言转商机同一事务内的调用顺序）。
/// </summary>
internal sealed class FakeLeadRepository : ILeadRepository
{
    private readonly Dictionary<Guid, Lead> _leads = [];
    private readonly List<string>? _calls;

    public FakeLeadRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>分页查询注入结果（未设置时返回空页）</summary>
    public (IReadOnlyList<LeadListItem> Items, int Total)? PagedResult { get; set; }

    /// <summary>最近一次分页查询入参，用于断言筛选透传</summary>
    public (string? Keyword, LeadSource? Source, LeadStatus? Status, Guid? OwnerId, int Page, int PageSize)? LastPagedArgs { get; set; }

    /// <summary>已执行的单号前缀序列（断言线索用 LD）</summary>
    public List<string> GeneratedPrefixes { get; } = [];

    /// <summary>负责人姓名表（按员工 id 解析，模拟联查 Employees）</summary>
    public Dictionary<Guid, string> OwnerNames { get; } = [];

    /// <summary>已存线索（供断言）</summary>
    public IReadOnlyCollection<Lead> Leads => _leads.Values;

    /// <summary>预置一条线索（供详情 / 编辑 / 状态流转 / 转商机用例）</summary>
    public void Seed(Lead lead) => _leads[lead.Id] = lead;

    public Task<(IReadOnlyList<LeadListItem> Items, int Total)> GetPagedAsync(
        string? keyword, LeadSource? source, LeadStatus? status, Guid? ownerId,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetPaged");
        LastPagedArgs = (keyword, source, status, ownerId, page, pageSize);
        if (PagedResult is not null)
        {
            return Task.FromResult(PagedResult.Value);
        }

        IReadOnlyList<LeadListItem> empty = Array.Empty<LeadListItem>();
        return Task.FromResult((empty, 0));
    }

    public Task<LeadDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_leads.TryGetValue(id, out var lead))
        {
            return Task.FromResult<LeadDetail?>(null);
        }

        return Task.FromResult<LeadDetail?>(new LeadDetail { Lead = lead, OwnerName = OwnerNameOf(lead) });
    }

    public Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        _leads[lead.Id] = lead;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Update");
        _leads[lead.Id] = lead;
        return Task.CompletedTask;
    }

    public Task<string> GenerateNoAsync(string prefix, DateTimeOffset leadDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        GeneratedPrefixes.Add(prefix);
        var pattern = $"{prefix}{leadDate.UtcDateTime:yyyyMMdd}";
        var seq = _leads.Values.Count(l => l.LeadNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }

    private string? OwnerNameOf(Lead lead)
        => lead.OwnerId is not null && OwnerNames.TryGetValue(lead.OwnerId.Value, out var name) ? name : null;
}

/// <summary>
/// 行为型商机仓储假实现（内存存储 + 记录调用），与 <see cref="FakeLeadRepository"/> 对称。
/// </summary>
internal sealed class FakeOpportunityRepository : IOpportunityRepository
{
    private readonly Dictionary<Guid, Opportunity> _opportunities = [];
    private readonly List<string>? _calls;

    public FakeOpportunityRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>分页查询注入结果（未设置时返回空页）</summary>
    public (IReadOnlyList<OpportunityListItem> Items, int Total)? PagedResult { get; set; }

    /// <summary>最近一次分页查询入参，用于断言筛选透传</summary>
    public (string? Keyword, OpportunityStage? Stage, Guid? PartnerId, Guid? OwnerId, int Page, int PageSize)? LastPagedArgs { get; set; }

    /// <summary>已执行的单号前缀序列（断言商机用 OP）</summary>
    public List<string> GeneratedPrefixes { get; } = [];

    /// <summary>负责人姓名表（按员工 id 解析，模拟联查 Employees）</summary>
    public Dictionary<Guid, string> OwnerNames { get; } = [];

    /// <summary>已存商机（供断言）</summary>
    public IReadOnlyCollection<Opportunity> Opportunities => _opportunities.Values;

    /// <summary>预置一条商机（供详情 / 编辑 / 阶段推进用例）</summary>
    public void Seed(Opportunity opportunity) => _opportunities[opportunity.Id] = opportunity;

    public Task<(IReadOnlyList<OpportunityListItem> Items, int Total)> GetPagedAsync(
        string? keyword, OpportunityStage? stage, Guid? partnerId, Guid? ownerId,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetPaged");
        LastPagedArgs = (keyword, stage, partnerId, ownerId, page, pageSize);
        if (PagedResult is not null)
        {
            return Task.FromResult(PagedResult.Value);
        }

        IReadOnlyList<OpportunityListItem> empty = Array.Empty<OpportunityListItem>();
        return Task.FromResult((empty, 0));
    }

    public Task<OpportunityDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_opportunities.TryGetValue(id, out var opportunity))
        {
            return Task.FromResult<OpportunityDetail?>(null);
        }

        return Task.FromResult<OpportunityDetail?>(
            new OpportunityDetail { Opportunity = opportunity, OwnerName = OwnerNameOf(opportunity) });
    }

    public Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        _opportunities[opportunity.Id] = opportunity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Update");
        _opportunities[opportunity.Id] = opportunity;
        return Task.CompletedTask;
    }

    public Task<string> GenerateNoAsync(string prefix, DateTimeOffset opportunityDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        GeneratedPrefixes.Add(prefix);
        var pattern = $"{prefix}{opportunityDate.UtcDateTime:yyyyMMdd}";
        var seq = _opportunities.Values.Count(o => o.OpportunityNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }

    private string? OwnerNameOf(Opportunity opportunity)
        => opportunity.OwnerId is not null && OwnerNames.TryGetValue(opportunity.OwnerId.Value, out var name) ? name : null;
}

/// <summary>
/// 行为型跟进活动仓储假实现（只增 + 按归属查询；记录人显示名按用户 id 解析）。
/// </summary>
internal sealed class FakeActivityRepository : IActivityRepository
{
    private readonly List<Activity> _activities = [];
    private readonly List<string>? _calls;

    public FakeActivityRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>记录人显示名表（按用户 id 解析，模拟联查 Users）</summary>
    public Dictionary<Guid, string> RecorderNames { get; } = [];

    /// <summary>已存活动（供断言）</summary>
    public IReadOnlyCollection<Activity> Activities => _activities;

    /// <summary>预置一条活动（供查询用例；绕过 AddAsync 的调用记录）</summary>
    public void Seed(Activity activity) => _activities.Add(activity);

    public Task<IReadOnlyList<ActivityItem>> GetByBizAsync(
        ActivityBizType bizType, Guid bizId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetByBiz");
        IReadOnlyList<ActivityItem> items = _activities
            .Where(a => a.BizType == bizType && a.BizId == bizId)
            .OrderByDescending(a => a.ActivityTime)
            .ThenByDescending(a => a.Id)
            .Select(a => new ActivityItem
            {
                Activity = a,
                RecorderName = a.CreatedBy is not null && RecorderNames.TryGetValue(a.CreatedBy.Value, out var name) ? name : null,
            })
            .ToList();
        return Task.FromResult(items);
    }

    public Task AddAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        _activities.Add(activity);
        return Task.CompletedTask;
    }
}
