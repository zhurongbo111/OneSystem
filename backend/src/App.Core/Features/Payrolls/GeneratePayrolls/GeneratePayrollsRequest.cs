using App.Core.Abstractions;

namespace App.Core.Features.Payrolls.GeneratePayrolls;

/// <summary>
/// 批量生成工资单请求（按期间为在职员工生成草稿；Query 参数绑定）
/// </summary>
public sealed class GeneratePayrollsRequest : IRequest<GeneratePayrollsResponse>
{
    /// <summary>年（2000–2100）</summary>
    public int Year { get; init; }

    /// <summary>月（1–12）</summary>
    public int Month { get; init; }
}
