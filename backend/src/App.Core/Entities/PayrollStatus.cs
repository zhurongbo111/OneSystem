namespace App.Core.Entities;

/// <summary>
/// 工资单状态（<c>specs/044-erp-hcm-payroll/design.md</c> §0.1）。
/// <see cref="Paid"/> 为发放终态：禁止编辑 / 删除（反发放走独立的发放接口）。
/// </summary>
public enum PayrollStatus
{
    /// <summary>草稿</summary>
    Draft = 0,

    /// <summary>已发放</summary>
    Paid = 1,
}
