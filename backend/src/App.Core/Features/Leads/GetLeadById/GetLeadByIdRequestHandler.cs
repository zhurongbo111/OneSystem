using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Leads.GetLeadById;

/// <summary>
/// 线索详情查询用例：取线索（含负责人姓名），不存在报 40400
/// </summary>
public sealed class GetLeadByIdRequestHandler : IRequestHandler<GetLeadByIdRequest, LeadDetailDto>
{
    private readonly ILeadRepository _leadRepository;

    /// <summary>
    /// 初始化线索详情查询用例处理器
    /// </summary>
    public GetLeadByIdRequestHandler(ILeadRepository leadRepository)
    {
        _leadRepository = leadRepository;
    }

    /// <summary>
    /// 处理线索详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LeadDetailDto> HandleAsync(GetLeadByIdRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _leadRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索不存在");
        }

        return LeadsDtoMapper.ToLeadDetailDto(detail);
    }
}
