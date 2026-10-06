namespace App.Core.Entities;

/// <summary>
/// 跟进活动字段约束常量（单一来源，specs/043-erp-crm-presale design.md §2.3 / §2.4）：
/// EF 配置（<c>ActivityConfiguration</c>）与新增活动用例 RequestValidator 均取本类常量，禁止硬编码字面量。
/// </summary>
public static class ActivityFieldConstraints
{
    /// <summary>跟进内容最大长度（列 <c>varchar(200)</c>）</summary>
    public const int ContentMaxLength = 200;
}
