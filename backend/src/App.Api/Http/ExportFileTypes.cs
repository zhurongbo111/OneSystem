namespace App.Api.Http;

/// <summary>
/// 文件下载类响应的 Content-Type 常量（唯一来源）。
/// 契约例外（成功返回二进制流、失败仍返回统一响应 JSON）见 specs/027-erp-export/design.md §0.1。
/// </summary>
public static class ExportFileTypes
{
    /// <summary>xlsx（Office Open XML 工作簿）</summary>
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
