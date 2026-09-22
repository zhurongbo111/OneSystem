using App.Core.Entities;

namespace App.Core.Audit;

/// <summary>
/// 字段级差异构建器：链式 <see cref="Add"/> 收集变更，<see cref="Build"/> 产出可直接入库的 JSON 文本。
/// 三条硬性规则（集中实现，不依赖各域自觉，详见 <c>specs/029-erp-audit-log/design.md</c> §0.2）：
/// ① 敏感字段名命中黑名单的变更一律丢弃；② 前后值相同的字段不记录；
/// ③ 超出 <see cref="AuditLogFieldConstraints.ChangesMaxLength"/> 时从尾部丢弃并设置 <see cref="Truncated"/>。
/// </summary>
public sealed class AuditChangeBuilder
{
    /// <summary>单个变更值文本上限；防止单行超长字段把 JSON 撑爆</summary>
    private const int MaxValueLength = 2000;

    /// <summary>命中即视为敏感字段的片段（小写包含判定）</summary>
    private static readonly string[] _sensitiveFragments = ["password", "token", "secret", "credential"];

    private readonly List<AuditChangeItem> _items = [];

    /// <summary>已收集的变更项数</summary>
    public int Count => _items.Count;

    /// <summary>本次构建的 JSON 是否因超长被截断</summary>
    public bool Truncated { get; private set; }

    /// <summary>
    /// 追加一项字段变更；字段敏感或前后值相同时跳过（不记录）
    /// </summary>
    /// <param name="field">实体字段名（camelCase）</param>
    /// <param name="label">中文字段名</param>
    /// <param name="before">变更前文本</param>
    /// <param name="after">变更后文本</param>
    /// <returns>构建器自身，支持链式调用</returns>
    public AuditChangeBuilder Add(string field, string label, string? before, string? after)
    {
        if (IsSensitive(field))
        {
            return this;
        }

        // 只记真正变化的字段：空值统一归一为 null 后再比对（null 与空串视为无变化）
        var beforeText = Normalize(before);
        var afterText = Normalize(after);
        if (string.Equals(beforeText, afterText, StringComparison.Ordinal))
        {
            return this;
        }

        _items.Add(new AuditChangeItem
        {
            Field = field,
            Label = label,
            Before = beforeText,
            After = afterText,
        });

        return this;
    }

    /// <summary>
    /// 判断字段名是否命中敏感字段黑名单（此项 change 永不落审计）
    /// </summary>
    /// <param name="field">实体字段名</param>
    public static bool IsSensitive(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return true;
        }

        var name = field.ToLowerInvariant();

        foreach (var fragment in _sensitiveFragments)
        {
            if (name.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // "xxxKey" 结尾视为密钥类字段；"permissionKeys" 等复数形式不在此列
        return name.EndsWith("key", StringComparison.Ordinal);
    }

    /// <summary>
    /// 构建变更差异 JSON 文本；无字段变更时返回 <c>null</c>（列存 NULL 而非空数组）
    /// </summary>
    public string? Build()
    {
        while (_items.Count > 0)
        {
            var json = AuditChangeSerializer.Serialize(_items);
            if (json.Length <= AuditLogFieldConstraints.ChangesMaxLength)
            {
                return json;
            }

            // 超长：从尾部丢弃变更项并在摘要标注（见 IAuditLogger 实现）
            Truncated = true;
            _items.RemoveAt(_items.Count - 1);
        }

        return null;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxValueLength ? trimmed : trimmed[..MaxValueLength];
    }
}
