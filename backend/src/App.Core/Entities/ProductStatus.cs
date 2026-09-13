namespace App.Core.Entities;

/// <summary>
/// 商品状态：停用后不能用于新单据选择，但保留全部历史数据（商品只停用不删除）
/// </summary>
public enum ProductStatus
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}
