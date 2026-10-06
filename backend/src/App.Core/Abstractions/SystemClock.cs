namespace App.Core.Abstractions;

/// <summary>
/// 系统时钟默认实现：读取 <c>DateTimeOffset.UtcNow</c>（040）
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset Today => new(UtcNow.UtcDateTime.Date, TimeSpan.Zero);
}
