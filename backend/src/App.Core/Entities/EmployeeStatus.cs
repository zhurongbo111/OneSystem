namespace App.Core.Entities;

/// <summary>
/// 员工在职状态：离职不删除记录（保留历史与审计引用，与 009 用户「禁用以代删除」一致）
/// （specs/030-erp-org-employee/design.md §0.4）
/// </summary>
public enum EmployeeStatus
{
    /// <summary>离职</summary>
    Resigned = 0,

    /// <summary>在职</summary>
    Active = 1,
}
