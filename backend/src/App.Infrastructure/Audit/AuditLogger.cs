using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;

namespace App.Infrastructure.Audit;

/// <summary>
/// <see cref="IAuditLogger"/> 的 EF Core 实现：把入参补齐操作人快照后交给审计仓储追加写入。
/// 不开启事务边界——沿用调用方既有的事务，业务异常回滚时日志一起回滚（失败不留痕）。
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    /// <summary>差异超长时在摘要尾部追加的标注</summary>
    private const string TruncatedMarker = "（变更内容过长已截断）";

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化审计日志写入器
    /// </summary>
    public AuditLogger(IAuditLogRepository auditLogRepository, ICurrentUser currentUser)
    {
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId(),
            Username = NullIfWhiteSpace(_currentUser.Username),
            DisplayName = NullIfWhiteSpace(_currentUser.DisplayName),
            Resource = entry.Resource,
            Action = entry.Action,
            ResourceId = entry.ResourceId,
            ResourceNo = NullIfWhiteSpace(entry.ResourceNo),
            Summary = BuildSummary(entry),
            Changes = entry.Changes,
            CreatedAt = entry.UtcNow,
        }, cancellationToken);
    }

    /// <summary>摘要 = 原文（必要时追加截断标注），超长按列长截断</summary>
    private static string BuildSummary(AuditEntry entry)
    {
        var summary = entry.Summary ?? string.Empty;
        if (entry.ChangesTruncated)
        {
            summary += TruncatedMarker;
        }

        return summary.Length <= AuditLogFieldConstraints.SummaryMaxLength
            ? summary
            : summary[..AuditLogFieldConstraints.SummaryMaxLength];
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
