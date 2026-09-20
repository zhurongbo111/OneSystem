namespace App.Core.Exports;

/// <summary>
/// 导出下载文件名构造（唯一来源）：<c>&lt;域&gt;_&lt;yyyyMMddHHmm&gt;.xlsx</c>，
/// 由 Controller 经 <c>Content-Disposition</c> 输出（UTF-8 编码，见 specs/027-erp-export/design.md §0.1）。
/// </summary>
public static class ExportFileNames
{
    /// <summary>
    /// 构造下载文件名：时间取导出当刻（UTC，格式 yyyyMMddHHmm）。
    /// </summary>
    /// <param name="domainName">中文域名（取 <see cref="ExportDomainNames"/>）</param>
    /// <param name="timestamp">导出当刻时间</param>
    public static string Build(string domainName, DateTimeOffset timestamp)
        => $"{domainName}_{timestamp.UtcDateTime:yyyyMMddHHmm}.xlsx";
}
