namespace App.Core.Entities;

/// <summary>
/// 性别（可空，未填时为 <see cref="Unknown"/>）
/// （specs/030-erp-org-employee/design.md §0.4）
/// </summary>
public enum Gender
{
    /// <summary>未填</summary>
    Unknown = 0,

    /// <summary>男</summary>
    Male = 1,

    /// <summary>女</summary>
    Female = 2,
}
