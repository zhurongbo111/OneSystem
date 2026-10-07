namespace App.Core.Entities;

/// <summary>
/// 考勤类型（<c>specs/044-erp-hcm-payroll/design.md</c> §0.1）。
/// 一期只做请假 / 加班**登记**，不引入打卡与排班模型。
/// </summary>
public enum AttendanceType
{
    /// <summary>请假</summary>
    Leave = 0,

    /// <summary>加班</summary>
    Overtime = 1,
}
