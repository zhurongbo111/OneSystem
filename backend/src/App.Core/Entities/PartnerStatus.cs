namespace App.Core.Entities;

/// <summary>
/// 往来单位状态（停用 / 启用；停用后不可被新单据选择，保留历史引用）
/// </summary>
public enum PartnerStatus
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}
