namespace App.Core.Entities;

/// <summary>
/// 工资单字段约束的**单一来源**：EF 实体配置与各 <c>RequestValidator</c> 均引用本类常量
/// （<c>specs/044-erp-hcm-payroll/design.md</c> §2.3，后端规则 §5.3）。
/// </summary>
public static class PayrollFieldConstraints
{
    /// <summary>金额上界（对齐 Payrolls 金额列 numeric(18,2) 的业务上界；下界固定为 0）</summary>
    public const decimal AmountMaxValue = 9999999.99m;

    /// <summary>备注最大长度（对齐 Payrolls.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;
}
