namespace App.Core.Features.Payrolls;

/// <summary>
/// 工资单金额口径的**单一来源**（<c>specs/044-erp-hcm-payroll/design.md</c> §0.1）：
/// <c>NetPay = BaseSalary + Allowance − Deduction</c>。
/// 新增 / 编辑 / 批量生成三条路径共用，禁止各自内联同一算式。
/// </summary>
internal static class PayrollAmountCalculator
{
    /// <summary>
    /// 计算实发金额（后端口径，不信任前端传入值）
    /// </summary>
    /// <param name="baseSalary">基本工资</param>
    /// <param name="allowance">津贴</param>
    /// <param name="deduction">扣款</param>
    public static decimal NetPay(decimal baseSalary, decimal allowance, decimal deduction)
        => baseSalary + allowance - deduction;
}
