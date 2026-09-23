using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.BankAccounts.UpdateBankAccountStatus;

/// <summary>
/// 资金账户启用 / 停用请求
/// </summary>
public sealed class UpdateBankAccountStatusRequest : IRequest<BankAccountDetailDto>
{
    /// <summary>资金账户 id（取自路由，请求体缺省时由 Controller 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; } = (int)BankAccountStatus.Enabled;
}
