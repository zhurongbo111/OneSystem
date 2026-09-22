namespace App.Core.Entities;

/// <summary>
/// 岗位相关字段约束的**单一来源**：EF 实体配置与各 <c>RequestValidator</c> 均引用本类常量
/// （specs/030-erp-org-employee/design.md §2.4）。
/// 列表关键词按 <c>Positions.Name</c> 匹配，长度上限直接引用 <see cref="NameMaxLength"/>，不另立常量。
/// </summary>
public static class PositionFieldConstraints
{
    /// <summary>岗位编码最大长度（对齐 Positions.Code varchar(20)）</summary>
    public const int CodeMaxLength = 20;

    /// <summary>岗位名称最大长度（对齐 Positions.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>备注最大长度（对齐 Positions.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;
}
