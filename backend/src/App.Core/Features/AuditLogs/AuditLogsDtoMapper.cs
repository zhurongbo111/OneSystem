using App.Core.Audit;
using App.Core.Entities;

using Microsoft.Extensions.Logging;

namespace App.Core.Features.AuditLogs;

/// <summary>
/// 操作日志实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）。
/// 差异 JSON 由后端反序列化为结构化数组返回，前端不再解析字符串；坏 JSON 只记 warning 不打断查询。
/// </summary>
internal static class AuditLogsDtoMapper
{
    /// <summary>映射列表项出参（不读取差异列）</summary>
    /// <param name="log">日志实体</param>
    public static AuditLogListItemDto ToAuditLogListItemDto(AuditLog log)
        => new()
        {
            Id = log.Id.ToString(),
            Username = log.Username,
            DisplayName = log.DisplayName,
            Resource = (int)log.Resource,
            Action = (int)log.Action,
            ResourceNo = log.ResourceNo,
            Summary = log.Summary,
            CreatedAt = log.CreatedAt,
        };

    /// <summary>映射详情出参（含结构化差异）</summary>
    /// <param name="log">日志实体</param>
    /// <param name="logger">反序列化失败时的告警日志；可空（单测场景）</param>
    public static AuditLogDetailDto ToAuditLogDetailDto(AuditLog log, ILogger? logger)
        => new()
        {
            Id = log.Id.ToString(),
            UserId = log.UserId?.ToString(),
            Username = log.Username,
            DisplayName = log.DisplayName,
            Resource = (int)log.Resource,
            Action = (int)log.Action,
            ResourceId = log.ResourceId?.ToString(),
            ResourceNo = log.ResourceNo,
            Summary = log.Summary,
            CreatedAt = log.CreatedAt,
            Changes = ToChangeDtos(log, logger),
        };

    private static IReadOnlyList<AuditChangeDto> ToChangeDtos(AuditLog log, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(log.Changes))
        {
            return [];
        }

        var items = AuditChangeSerializer.TryDeserialize(log.Changes);
        if (items is null)
        {
            logger?.LogWarning("审计日志 {AuditLogId} 的差异 JSON 反序列化失败，已按无差异返回", log.Id);
            return [];
        }

        return items
            .Select(x => new AuditChangeDto { Field = x.Field, Label = x.Label, Before = x.Before, After = x.After })
            .ToList();
    }
}
