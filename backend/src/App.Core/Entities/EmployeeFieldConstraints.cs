namespace App.Core.Entities;

/// <summary>
/// 员工相关字段约束的**单一来源**：EF 实体配置与各 <c>RequestValidator</c> 均引用本类常量
/// （specs/030-erp-org-employee/design.md §2.4）。
/// 手机号格式与用户域同源引用（同一条业务规则，禁止复制正则）。
/// </summary>
public static class EmployeeFieldConstraints
{
    /// <summary>工号最大长度（对齐 Employees.EmployeeNo varchar(20)）</summary>
    public const int NoMaxLength = 20;

    /// <summary>姓名最大长度（对齐 Employees.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>手机号最大长度（对齐 Employees.Phone varchar(20)）</summary>
    public const int PhoneMaxLength = 20;

    /// <summary>邮箱最大长度（对齐 Employees.Email varchar(100)）</summary>
    public const int EmailMaxLength = 100;

    /// <summary>备注最大长度（对齐 Employees.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>手机号格式：中国大陆手机号（同源 <see cref="UserFieldConstraints.PhonePattern"/>）</summary>
    public const string PhonePattern = UserFieldConstraints.PhonePattern;
}
