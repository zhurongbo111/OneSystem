using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountMappings.UpdateAccountMappings;

/// <summary>
/// 科目映射维护请求（全量覆盖：须提交全部映射键）
/// </summary>
public sealed class UpdateAccountMappingsRequest : IRequest<IReadOnlyList<AccountMappingDto>>
{
    /// <summary>映射项集合（键须与 <c>AccountMappingKeys.All</c> 完全一致）</summary>
    public IReadOnlyList<UpdateAccountMappingItem> Items { get; init; } = [];
}

/// <summary>科目映射维护项（键 → 目标科目）</summary>
public sealed class UpdateAccountMappingItem
{
    /// <summary>映射键</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>目标科目 id（须为末级且启用）</summary>
    public Guid AccountId { get; init; }
}
