using App.Core.Errors;

namespace App.Core.Exports;

/// <summary>
/// 导出用例的通用业务判定（供各域导出 Handler 复用）；格式校验仍在各用例 RequestValidator。
/// </summary>
public static class ExportGuards
{
    /// <summary>
    /// 校验单个工作表的数据行数未超过导出上限（<see cref="ExportFieldConstraints.MaxRows"/>），超限抛 40000。
    /// 调用方按「MaxRows + 1」多取一行用于判定，保证超限是明确报错而非静默截断。
    /// </summary>
    /// <param name="rowCount">本工作表实际取到的数据行数</param>
    public static void EnsureWithinRowLimit(int rowCount)
    {
        if (rowCount > ExportFieldConstraints.MaxRows)
        {
            throw new BusinessException(ErrorCode.Validation, ExportFieldConstraints.MaxRowsExceededMessage);
        }
    }
}
