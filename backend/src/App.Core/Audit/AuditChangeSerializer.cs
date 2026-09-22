using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Core.Audit;

/// <summary>
/// 审计差异 JSON 的序列化**单一事实源**：写入端（<see cref="AuditChangeBuilder"/>）与读取端（审计详情映射）共用同一套命名策略，
/// 避免字段名 / 大小写漂移导致详情反序列化为空。反序列化失败返回 <c>null</c>，由调用方兜底（坏数据不打断查询）。
/// </summary>
internal static class AuditChangeSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // 中文标签与值不转义：落库的差异 JSON 直接在 psql 里可读
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>把差异项序列化为待落库的 JSON 数组文本</summary>
    /// <param name="items">差异项</param>
    public static string Serialize(IReadOnlyList<AuditChangeItem> items) => JsonSerializer.Serialize(items, _options);

    /// <summary>
    /// 反序列化差异 JSON；入参为空或 JSON 非法时返回 <c>null</c>（调用方按需记日志）
    /// </summary>
    /// <param name="json">差异 JSON 文本</param>
    public static IReadOnlyList<AuditChangeItem>? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<AuditChangeItem>>(json, _options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
