using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Approvals.GetApprovalById;

/// <summary>
/// 审批详情查询用例：取审批记录（不存在 → 40400）→ 组装被审批单据摘要与明细（<see cref="ApprovalDetailBuilder"/>）
/// </summary>
public sealed class GetApprovalByIdRequestHandler : IRequestHandler<GetApprovalByIdRequest, ApprovalDetailDto>
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly ApprovalDetailBuilder _detailBuilder;

    /// <summary>
    /// 初始化审批详情查询用例处理器
    /// </summary>
    public GetApprovalByIdRequestHandler(
        IApprovalRepository approvalRepository, ApprovalDetailBuilder detailBuilder)
    {
        _approvalRepository = approvalRepository;
        _detailBuilder = detailBuilder;
    }

    /// <summary>
    /// 处理审批详情查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ApprovalDetailDto> HandleAsync(GetApprovalByIdRequest request, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (approval is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "审批记录不存在");
        }

        return await _detailBuilder.BuildAsync(approval, cancellationToken);
    }
}
