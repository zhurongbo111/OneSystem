using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型服务工单仓储假实现（内存存储 + 记录调用）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（045 design.md §6）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录调用序列（断言同一事务内的调用顺序）。
/// </summary>
internal sealed class FakeServiceTicketRepository : IServiceTicketRepository
{
    private readonly Dictionary<Guid, ServiceTicket> _tickets = [];
    private readonly List<string>? _calls;

    public FakeServiceTicketRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>分页查询注入结果（未设置时返回空页）</summary>
    public (IReadOnlyList<ServiceTicketListItem> Items, int Total)? PagedResult { get; set; }

    /// <summary>最近一次分页查询入参，用于断言筛选透传</summary>
    public (string? Keyword, TicketStatus? Status, TicketPriority? Priority, Guid? OwnerId, int Page, int PageSize)? LastPagedArgs { get; set; }

    /// <summary>已执行的单号前缀序列（断言工单用 SV）</summary>
    public List<string> GeneratedPrefixes { get; } = [];

    /// <summary>负责人姓名表（按员工 id 解析，模拟联查 Employees）</summary>
    public Dictionary<Guid, string> OwnerNames { get; } = [];

    /// <summary>已存工单（供断言）</summary>
    public IReadOnlyCollection<ServiceTicket> Tickets => _tickets.Values;

    /// <summary>预置一条工单（供详情 / 编辑 / 状态流转 / 指派用例）</summary>
    public void Seed(ServiceTicket ticket) => _tickets[ticket.Id] = ticket;

    public Task<(IReadOnlyList<ServiceTicketListItem> Items, int Total)> GetPagedAsync(
        string? keyword, TicketStatus? status, TicketPriority? priority, Guid? ownerId,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetPaged");
        LastPagedArgs = (keyword, status, priority, ownerId, page, pageSize);
        if (PagedResult is not null)
        {
            return Task.FromResult(PagedResult.Value);
        }

        IReadOnlyList<ServiceTicketListItem> empty = Array.Empty<ServiceTicketListItem>();
        return Task.FromResult((empty, 0));
    }

    public Task<ServiceTicketDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tickets.TryGetValue(id, out var ticket))
        {
            return Task.FromResult<ServiceTicketDetail?>(null);
        }

        return Task.FromResult<ServiceTicketDetail?>(
            new ServiceTicketDetail { Ticket = ticket, OwnerName = OwnerNameOf(ticket) });
    }

    public Task AddAsync(ServiceTicket ticket, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        _tickets[ticket.Id] = ticket;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ServiceTicket ticket, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Update");
        _tickets[ticket.Id] = ticket;
        return Task.CompletedTask;
    }

    public Task<string> GenerateNoAsync(string prefix, DateTimeOffset ticketDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        GeneratedPrefixes.Add(prefix);
        var pattern = $"{prefix}{ticketDate.UtcDateTime:yyyyMMdd}";
        var seq = _tickets.Values.Count(t => t.TicketNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }

    private string? OwnerNameOf(ServiceTicket ticket)
        => ticket.OwnerId is not null && OwnerNames.TryGetValue(ticket.OwnerId.Value, out var name) ? name : null;
}
