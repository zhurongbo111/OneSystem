using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Approvals.GetApprovals;

/// <summary>
/// 审批记录分页查询用例：状态 / 类型 / 提交人 / 时间筛选 + 提交时间倒序；
/// 提交人与审批人显示名一次批量解析（避免逐行往返）。
/// </summary>
public sealed class GetApprovalsRequestHandler : IRequestHandler<GetApprovalsRequest, PagedResult<ApprovalListItemDto>>
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// 初始化审批记录分页查询用例处理器
    /// </summary>
    public GetApprovalsRequestHandler(IApprovalRepository approvalRepository, IUserRepository userRepository)
    {
        _approvalRepository = approvalRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// 处理审批记录分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<ApprovalListItemDto>> HandleAsync(
        GetApprovalsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _approvalRepository.GetPagedAsync(
            request.Status,
            request.OrderType,
            request.SubmittedBy,
            request.Start,
            request.End,
            request.Page,
            request.PageSize,
            cancellationToken);

        // 提交人 + 审批人显示名一次批量取回（仓储契约：缺失 id 不出现）
        var userIds = items.Select(a => a.SubmittedBy)
            .Concat(items.Where(a => a.DecidedBy is not null).Select(a => a.DecidedBy!.Value))
            .Distinct()
            .ToList();
        var names = await _userRepository.GetDisplayNamesByIdsAsync(userIds, cancellationToken);

        return new PagedResult<ApprovalListItemDto>
        {
            Items = items
                .Select(a => ApprovalsDtoMapper.ToApprovalListItemDto(
                    a,
                    NameOf(names, a.SubmittedBy),
                    a.DecidedBy is null ? null : NameOf(names, a.DecidedBy.Value)))
                .ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    private static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid userId)
        => names.TryGetValue(userId, out var name) ? name : string.Empty;
}
