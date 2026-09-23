using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.BankAccounts;

/// <summary>
/// 资金账户出参映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class BankAccountDtoMapper
{
    /// <summary>资金账户列表读模型 → 列表项出参</summary>
    public static BankAccountListItemDto ToBankAccountListItemDto(BankAccountListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            Type = (int)item.Type,
            BankName = item.BankName,
            AccountNo = item.AccountNo,
            InitialBalance = item.InitialBalance,
            Balance = item.Balance,
            Status = (int)item.Status,
            Remark = item.Remark,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };

    /// <summary>资金账户实体 → 详情出参</summary>
    public static BankAccountDetailDto ToBankAccountDetailDto(BankAccount bankAccount)
        => new()
        {
            Id = bankAccount.Id.ToString(),
            Code = bankAccount.Code,
            Name = bankAccount.Name,
            Type = (int)bankAccount.Type,
            BankName = bankAccount.BankName,
            AccountNo = bankAccount.AccountNo,
            InitialBalance = bankAccount.InitialBalance,
            Status = (int)bankAccount.Status,
            Remark = bankAccount.Remark,
            CreatedAt = bankAccount.CreatedAt,
            UpdatedAt = bankAccount.UpdatedAt,
        };

    /// <summary>资金账户余额读模型 → 余额出参</summary>
    public static BankAccountBalanceItemDto ToBankAccountBalanceItemDto(BankAccountBalanceItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            Type = (int)item.Type,
            Status = (int)item.Status,
            Balance = item.Balance,
        };
}
