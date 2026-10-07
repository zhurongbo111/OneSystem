namespace App.Core.Features.Payrolls;

/// <summary>
/// 工资单列表项出参（薪酬列表页）。
/// 枚举以**整型**输出（PayrollStatus：0 草稿 / 1 已发放）；<see cref="StatusText"/> 为派生文案。
/// </summary>
public sealed class PayrollListItemDto
{
    /// <summary>工资单 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>员工 ID</summary>
    public string EmployeeId { get; init; } = string.Empty;

    /// <summary>员工姓名（快照）</summary>
    public string EmployeeName { get; init; } = string.Empty;

    /// <summary>年</summary>
    public int Year { get; init; }

    /// <summary>月</summary>
    public int Month { get; init; }

    /// <summary>基本工资</summary>
    public decimal BaseSalary { get; init; }

    /// <summary>津贴</summary>
    public decimal Allowance { get; init; }

    /// <summary>扣款</summary>
    public decimal Deduction { get; init; }

    /// <summary>实发（后端计算）</summary>
    public decimal NetPay { get; init; }

    /// <summary>状态（0 草稿 / 1 已发放）</summary>
    public int Status { get; init; }

    /// <summary>状态文案（草稿 / 已发放）</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
