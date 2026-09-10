namespace App.Core.Entities;

/// <summary>
/// 用户状态：禁用后不能登录，但保留全部历史数据（本期不提供删除）
/// </summary>
public enum UserStatus
{
    /// <summary>禁用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}
