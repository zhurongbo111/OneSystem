namespace App.Core.Entities;

/// <summary>
/// 审计动作枚举（<c>specs/029-erp-audit-log/design.md</c> §0.3）。只覆盖写操作，查询 / 打印 / 导出不记录。
/// </summary>
public enum AuditAction
{
    /// <summary>创建</summary>
    Create = 0,

    /// <summary>更新</summary>
    Update = 1,

    /// <summary>删除</summary>
    Delete = 2,

    /// <summary>启停 / 状态变更</summary>
    StatusChange = 3,

    /// <summary>作废</summary>
    Void = 4,

    /// <summary>关闭</summary>
    Close = 5,

    /// <summary>结算 / 核销</summary>
    Settle = 6,

    /// <summary>审批（通过 / 驳回）</summary>
    Approve = 7,

    /// <summary>库存调整（期初建账 / 盘点）</summary>
    Adjust = 8,

    /// <summary>成本重算</summary>
    Recalculate = 9,
}
