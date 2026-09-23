namespace App.Core.Features.BankAccounts;

/// <summary>
/// 资金账户列表项出参模型。
/// 枚举统一以**整型**输出（`BankAccountType`：1 现金 / 2 银行；`BankAccountStatus`：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class BankAccountListItemDto
{
    /// <summary>资金账户 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>账户编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>账户名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>账户类型（1 现金 / 2 银行）</summary>
    public int Type { get; init; }

    /// <summary>开户行，可空</summary>
    public string? BankName { get; init; }

    /// <summary>银行账号，可空</summary>
    public string? AccountNo { get; init; }

    /// <summary>初始余额</summary>
    public decimal InitialBalance { get; init; }

    /// <summary>当前余额（初始余额 + Σ 收款 − Σ 付款，派生值）</summary>
    public decimal Balance { get; init; }

    /// <summary>账户状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
