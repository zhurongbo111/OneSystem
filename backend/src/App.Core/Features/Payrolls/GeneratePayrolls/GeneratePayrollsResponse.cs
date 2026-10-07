namespace App.Core.Features.Payrolls.GeneratePayrolls;

/// <summary>
/// 批量生成工资单出参：新增 / 跳过计数（已存在则跳过，重复执行不产生重复工资单）
/// </summary>
public sealed class GeneratePayrollsResponse
{
    /// <summary>本次新增的草稿条数</summary>
    public int Created { get; init; }

    /// <summary>因该期间已存在而跳过的条数</summary>
    public int Skipped { get; init; }
}
