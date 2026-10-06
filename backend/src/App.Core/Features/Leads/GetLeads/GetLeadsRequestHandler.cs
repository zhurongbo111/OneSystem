using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Leads.GetLeads;

/// <summary>
/// 线索分页查询用例：仓储分页筛选（关键词 / 来源 / 状态 / 负责人）→ 映射 DTO（含负责人姓名）
/// </summary>
public sealed class GetLeadsRequestHandler : IRequestHandler<GetLeadsRequest, PagedResult<LeadListItemDto>>
{
    private readonly ILeadRepository _leadRepository;

    /// <summary>
    /// 初始化线索分页查询用例处理器
    /// </summary>
    public GetLeadsRequestHandler(ILeadRepository leadRepository)
    {
        _leadRepository = leadRepository;
    }

    /// <summary>
    /// 处理线索分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<LeadListItemDto>> HandleAsync(GetLeadsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _leadRepository.GetPagedAsync(
            request.Keyword, request.Source, request.Status, request.OwnerId,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<LeadListItemDto>
        {
            Items = items.Select(LeadsDtoMapper.ToLeadListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
