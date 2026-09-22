using App.Core.Abstractions;

namespace App.Core.Features.Positions.GetPositionPicks;

/// <summary>
/// 岗位选择查询请求（空参数：仅启用岗位，全量返回，供员工表单下拉）
/// </summary>
public sealed class GetPositionPicksRequest : IRequest<IReadOnlyList<PositionPickDto>>
{
}
