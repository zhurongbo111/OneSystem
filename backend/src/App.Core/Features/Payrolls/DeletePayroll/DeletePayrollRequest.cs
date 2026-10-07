using App.Core.Abstractions;

namespace App.Core.Features.Payrolls.DeletePayroll;

/// <summary>
/// 删除工资单请求（仅草稿可删；已发放 `40171`）
/// </summary>
public sealed class DeletePayrollRequest : IRequest<object?>
{
    /// <summary>工资单 id（由路由提供）</summary>
    public Guid Id { get; init; }
}
