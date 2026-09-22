namespace App.Core.Features.GeneralLedger;

/// <summary>
/// 科目映射出参模型（键 + 中文标签 + 目标科目快照）。
/// 中文标签由后端返回（<c>AccountMappingKeys</c>），前端不硬编码
/// </summary>
public sealed class AccountMappingDto
{
    /// <summary>映射键</summary>
    public required string Key { get; init; }

    /// <summary>映射键中文标签</summary>
    public required string Label { get; init; }

    /// <summary>目标科目 id（未配置时为空字符串）</summary>
    public required string AccountId { get; init; }

    /// <summary>目标科目编码（未配置时为空字符串）</summary>
    public required string AccountCode { get; init; }

    /// <summary>目标科目名称（未配置时为空字符串）</summary>
    public required string AccountName { get; init; }
}
