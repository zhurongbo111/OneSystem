namespace App.Core.Entities;

/// <summary>
/// 考勤登记字段约束的**单一来源**：EF 实体配置与各 <c>RequestValidator</c> 均引用本类常量
/// （<c>specs/044-erp-hcm-payroll/design.md</c> §2.3，后端规则 §5.3）。
/// </summary>
public static class AttendanceFieldConstraints
{
    /// <summary>事由最大长度（对齐 Attendances.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;
}
