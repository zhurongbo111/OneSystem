using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Payrolls.UpdatePayrollStatus;

/// <summary>
/// 工资单发放 / 反发放请求（发放后锁定；反发放为受限权限的后门）
/// </summary>
public sealed class UpdatePayrollStatusRequest : IRequest<PayrollDetailDto>
{
    /// <summary>工资单 id（由路由提供，请求体可缺省）</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 草稿 / 1 已发放）</summary>
    public int Status { get; init; } = (int)PayrollStatus.Paid;
}
