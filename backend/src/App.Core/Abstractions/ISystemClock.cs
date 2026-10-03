namespace App.Core.Abstractions;

/// <summary>
/// 系统时钟抽象（040 引入）：业务用例中涉及「今天」的判定（过期 / 近效期）一律经本接口取时间，
/// 不直接读 <c>DateTimeOffset.UtcNow</c>，单测可注入固定时间（specs/040-erp-batch-expiry/design.md §6）。
/// </summary>
public interface ISystemClock
{
    /// <summary>当前 UTC 时间</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// 当前 UTC 日期（UTC 午夜，offset=0）。业务「今天」判定一律取本属性，**禁止**用 <c>UtcNow.Date</c>：
    /// <c>DateTimeOffset.Date</c> 返回 <c>Kind=Unspecified</c> 的 <c>DateTime</c>，赋给 <c>DateTimeOffset</c>
    /// 参数 / 属性时隐式转换按服务器本地时区解释（偏移漂移，如 +08:00），Npgsql 绑定 timestamptz 参数
    /// 报 50000；本属性经 <c>UtcDateTime.Date</c> 显式构造为 UTC 午夜，单测可注入固定值（040）。
    /// </summary>
    DateTimeOffset Today { get; }
}
