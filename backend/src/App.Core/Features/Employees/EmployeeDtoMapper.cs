using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Employees;

/// <summary>
/// 员工出参映射（集中一处，避免各用例重复拼装）。
/// 派生字段：<c>statusText</c>（在职 / 离职）随映射一并计算；关联账号显示名由读模型带出。
/// </summary>
internal static class EmployeeDtoMapper
{
    /// <summary>员工列表读模型 → 列表项出参</summary>
    public static EmployeeListItemDto ToEmployeeListItemDto(EmployeeListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            EmployeeNo = item.EmployeeNo,
            Name = item.Name,
            Gender = item.Gender is null ? null : (int)item.Gender.Value,
            Phone = item.Phone,
            DepartmentId = item.DepartmentId?.ToString(),
            DepartmentName = item.DepartmentName,
            PositionId = item.PositionId?.ToString(),
            PositionName = item.PositionName,
            HireDate = item.HireDate,
            ResignDate = item.ResignDate,
            Status = (int)item.Status,
            StatusText = StatusText(item.Status),
            UserId = item.UserId?.ToString(),
            UserDisplayName = item.UserDisplayName,
            CreatedAt = item.CreatedAt,
        };

    /// <summary>员工详情读模型 → 详情出参</summary>
    public static EmployeeDetailDto ToEmployeeDetailDto(EmployeeDetail detail)
        => new()
        {
            Id = detail.Id.ToString(),
            EmployeeNo = detail.EmployeeNo,
            Name = detail.Name,
            Gender = detail.Gender is null ? null : (int)detail.Gender.Value,
            Phone = detail.Phone,
            Email = detail.Email,
            DepartmentId = detail.DepartmentId?.ToString(),
            DepartmentName = detail.DepartmentName,
            PositionId = detail.PositionId?.ToString(),
            PositionName = detail.PositionName,
            HireDate = detail.HireDate,
            ResignDate = detail.ResignDate,
            Status = (int)detail.Status,
            StatusText = StatusText(detail.Status),
            UserId = detail.UserId?.ToString(),
            UserDisplayName = detail.UserDisplayName,
            Remark = detail.Remark,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt,
        };

    /// <summary>员工可选账号读模型 → 出参</summary>
    public static EmployeePickUserDto ToEmployeePickUserDto(EmployeePickUserItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Username = item.Username,
            DisplayName = item.DisplayName,
        };

    /// <summary>在职状态文案</summary>
    private static string StatusText(EmployeeStatus status)
        => status == EmployeeStatus.Active ? "在职" : "离职";
}
