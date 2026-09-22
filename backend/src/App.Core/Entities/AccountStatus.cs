namespace App.Core.Entities;

/// <summary>
/// 会计科目状态：停用科目不可被新凭证引用（`033` 落地后生效），历史数据保留
/// （specs/031-erp-finance-master/design.md §0.1）
/// </summary>
public enum AccountStatus
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}