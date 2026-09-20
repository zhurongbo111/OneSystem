namespace App.Core.Exports;

/// <summary>
/// 导出相关约束常量（单一来源，禁止在用例 / 实现中硬编码字面量）。
/// </summary>
public static class ExportFieldConstraints
{
    /// <summary>
    /// 单次导出每个工作表的最大数据行数；超限返回 40000（提示缩小筛选范围），不静默截断。
    /// </summary>
    public const int MaxRows = 50000;

    /// <summary>
    /// 单次导出最大行数提示文案（超限错误信息，前端按统一响应提示）。
    /// </summary>
    public const string MaxRowsExceededMessage = "导出数据超过 50000 行，请缩小筛选范围后重试";
}
