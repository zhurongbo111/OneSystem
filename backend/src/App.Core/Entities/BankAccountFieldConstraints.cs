namespace App.Core.Entities;

/// <summary>
/// 资金账户相关字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c> / 精度 / 区间）与各 <c>RequestValidator</c>
/// 均引用本类常量，保证"格式校验"与"数据库约束"始终一致
/// （specs/034-erp-cash/design.md §2.3）。
/// 列表关键词按 <c>BankAccounts.Name</c> 匹配，长度上限直接引用 <see cref="NameMaxLength"/>，不另立常量。
/// </summary>
public static class BankAccountFieldConstraints
{
    /// <summary>账户编码最大长度（对齐 BankAccounts.Code varchar(20)）</summary>
    public const int CodeMaxLength = 20;

    /// <summary>账户名称最大长度（对齐 BankAccounts.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>开户行最大长度（对齐 BankAccounts.BankName varchar(100)）</summary>
    public const int BankNameMaxLength = 100;

    /// <summary>银行账号最大长度（对齐 BankAccounts.AccountNo varchar(30)）</summary>
    public const int AccountNoMaxLength = 30;

    /// <summary>备注最大长度（对齐 BankAccounts.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>初始余额最小值（不允许负初始余额）</summary>
    public const decimal InitialBalanceMin = 0m;

    /// <summary>初始余额最大值（对齐 numeric(18,2)）</summary>
    public const decimal InitialBalanceMax = 9999999999999999.99m;

    /// <summary>账户金额数值总位数（对齐 numeric(18,2)）</summary>
    public const int AmountPrecision = 18;

    /// <summary>账户金额数值小数位（对齐 numeric(18,2)）</summary>
    public const int AmountDecimalPlaces = 2;
}
