using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Accounts.UpdateAccount;

/// <summary>
/// 编辑会计科目请求（全量覆盖语义：缺字段 / 空串一律清空，AGENTS.md §4.5；
/// 编码与名称均可改，唯一性由 Handler 校验；<c>IsPreset</c> 不可改）
/// </summary>
public sealed class UpdateAccountRequest : IRequest<AccountDetailDto>
{
    /// <summary>科目 id（取自路由，请求体缺省时由 Controller 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>科目编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>科目名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>科目类别（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益）</summary>
    public int Category { get; init; } = (int)AccountCategory.Asset;

    /// <summary>余额方向（1 借 / 2 贷）</summary>
    public int Direction { get; init; } = (int)AccountDirection.Debit;

    /// <summary>上级科目 id，可空（<c>null</c> 表示一级科目）</summary>
    public Guid? ParentId { get; init; }

    /// <summary>同级排序</summary>
    public int SortOrder { get; init; }

    /// <summary>科目状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; } = (int)AccountStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}