namespace App.Core.Entities;

/// <summary>
/// 部门相关字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c> / 区间）与各 <c>RequestValidator</c>
/// 均引用本类常量，保证"格式校验"与"数据库约束"始终一致
/// （specs/030-erp-org-employee/design.md §2.4）。
/// </summary>
public static class DepartmentFieldConstraints
{
    /// <summary>部门编码最大长度（对齐 Departments.Code varchar(20)）</summary>
    public const int CodeMaxLength = 20;

    /// <summary>部门名称最大长度（对齐 Departments.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>备注最大长度（对齐 Departments.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>同级排序最小值</summary>
    public const int SortOrderMinValue = 0;

    /// <summary>同级排序最大值</summary>
    public const int SortOrderMaxValue = 9999;
}
