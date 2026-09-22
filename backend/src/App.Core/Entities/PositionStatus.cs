namespace App.Core.Entities;

/// <summary>
/// 岗位状态：停用岗位不参与员工选择，历史数据保留
/// （specs/030-erp-org-employee/design.md §0.4）
/// </summary>
public enum PositionStatus
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}
