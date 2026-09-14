using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.LoginLogs.GetLoginLogs;

/// <summary>
/// 登录日志分页查询用例：按登录名 / 登录时间范围筛选，登录时间倒序（只读）
/// </summary>
public sealed class GetLoginLogsRequestHandler : IRequestHandler<GetLoginLogsRequest, PagedResult<LoginLogListItemDto>>
{
    private readonly IUserLoginLogRepository _userLoginLogRepository;

    /// <summary>
    /// 初始化登录日志查询用例处理器
    /// </summary>
    public GetLoginLogsRequestHandler(IUserLoginLogRepository userLoginLogRepository)
    {
        _userLoginLogRepository = userLoginLogRepository;
    }

    /// <summary>
    /// 处理登录日志查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<LoginLogListItemDto>> HandleAsync(GetLoginLogsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _userLoginLogRepository.GetPagedAsync(
            request.Username,
            request.StartTime,
            request.EndTime,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<LoginLogListItemDto>
        {
            Items = items.Select(LoginLogDtoMapper.ToLoginLogListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
