namespace App.Core;

/// <summary>
/// 顺序 Guid 生成器（RFC 4122 v1 布局：时间字段小端 + 计数器位大端，采购 / 销售共用）：
/// 字节序与 PostgreSQL uuid btree 比较序一致（亦与 .NET Guid.CompareTo 一致），
/// 同一单据明细按行生成时字节字典序与生成顺序相同，
/// 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序，见 design.md §3.1）。
/// 同进程内 tick + 单调计数器保证唯一；跨进程计数器随机初值降低同 tick 碰撞概率（与 .NET 官方 NewSequentialGuid 同策略）。
/// </summary>
public static class SequentialGuidGenerator
{
    /// <summary>.NET Guid 纪元（2000-01-01T00:00:00Z）的 Ticks，RFC 4122 时间字段起点</summary>
    private const long EpochTicks = 0x01B21DD213814000L;

    /// <summary>62 位单调计数器（进程内原子递增；随机初值降低跨进程同 tick 碰撞概率）</summary>
    private static long _counter = Random.Shared.NextInt64();

    /// <summary>生成一个顺序 Guid（同一请求内多次调用，结果字节字典序与调用顺序一致）</summary>
    public static Guid NewSequential()
    {
        var now = (ulong)(DateTime.UtcNow.Ticks - EpochTicks);
        var seq = Interlocked.Increment(ref _counter) & 0x3FFFFFFFFFFFFFFFL; // 低 62 位

        // 字节布局（与 PG uuid 存储序 / .NET Guid 比较序一致）：
        // [0..3] timeLow 小端、[4..5] timeMid 小端、[6] timeHi(4bit) + version(0x1)
        // [7] variant(0x8) + 计数器高 6 位、[8..15] 计数器低 56 位大端
        var bytes = new byte[16];
        var timeLow = now & 0xFFFFFFFF;
        var timeMid = (now >> 32) & 0xFFFF;
        bytes[0] = (byte)timeLow;
        bytes[1] = (byte)(timeLow >> 8);
        bytes[2] = (byte)(timeLow >> 16);
        bytes[3] = (byte)(timeLow >> 24);
        bytes[4] = (byte)timeMid;
        bytes[5] = (byte)(timeMid >> 8);
        bytes[6] = (byte)(((now >> 48) & 0x0F) | 0x10); // version 1
        bytes[7] = (byte)(0x80 | ((seq >> 56) & 0x3F)); // variant 10
        for (var i = 15; i >= 8; i--)
        {
            bytes[i] = (byte)(seq & 0xFF);
            seq >>= 8;
        }

        return new Guid(bytes);
    }
}
