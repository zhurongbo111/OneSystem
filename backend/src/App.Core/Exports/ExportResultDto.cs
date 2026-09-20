namespace App.Core.Exports;

/// <summary>
/// 导出用例出参：文件名 + xlsx 二进制内容（各域导出用例共用）。
/// Controller 只负责把 <see cref="Content"/> 写为文件流响应，
/// 契约例外（成功返回二进制流、失败返回统一响应 JSON）见 specs/027-erp-export/design.md §0.1。
/// </summary>
public sealed class ExportResultDto
{
    /// <summary>下载文件名（含扩展名）</summary>
    public required string FileName { get; init; }

    /// <summary>xlsx 文件内容</summary>
    public required byte[] Content { get; init; }
}
