using App.Core.Abstractions;
using App.Core.Alerts;
using App.Core.Auth;
using App.Core.Entities;

using Microsoft.Extensions.Logging.Abstractions;

namespace App.Tests;

/// <summary>
/// 库存预警扫描器单测（041 §6）：三类信号 → 当日去重 → 生成消息 → 统计。
/// 扫描时刻由入参注入（不读时钟）；判定口径（阈值 / 窗口边界）由
/// <see cref="StockAlertQueryRepositoryTests"/> 用真实仓储覆盖，本类只覆盖编排与去重。
/// </summary>
public class StockAlertScannerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static StockAlertScanner CreateScanner(
        FakeStockAlertQueryRepository queryRepository,
        FakeAlertRecordRepository alertRecordRepository,
        FakePermissionedUserQuery permissionedUserQuery,
        FakeNotificationWriter notificationWriter)
        => new(
            queryRepository,
            alertRecordRepository,
            permissionedUserQuery,
            notificationWriter,
            NullLogger<StockAlertScanner>.Instance);

    private static StockAlertSignal LowStockSignal(Guid? productId = null, int quantity = 10, int safetyStock = 100)
        => new()
        {
            ProductId = productId ?? Guid.NewGuid(),
            ProductCode = "SKU-001",
            ProductName = "商品一",
            WarehouseId = Guid.NewGuid(),
            WarehouseName = "上海仓",
            Quantity = quantity,
            SafetyStock = safetyStock,
        };

    private static StockAlertSignal BatchSignal(
        DateTimeOffset expiryDate, Guid? productId = null, Guid? warehouseId = null, string batchNo = "B1")
        => new()
        {
            ProductId = productId ?? Guid.NewGuid(),
            ProductCode = "SKU-001",
            ProductName = "商品一",
            WarehouseId = warehouseId ?? Guid.NewGuid(),
            WarehouseName = "上海仓",
            Quantity = 5,
            SafetyStock = 0,
            BatchId = Guid.NewGuid(),
            BatchNo = batchNo,
            ExpiryDate = expiryDate,
        };

    [Fact]
    public async Task 扫描_三类信号与多接收人_应逐人生成消息并写台账()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        queryRepository.LowStock.Add(LowStockSignal());
        queryRepository.Expiring.Add(BatchSignal(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero)));
        queryRepository.Expired.Add(BatchSignal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        var alertRecordRepository = new FakeAlertRecordRepository();
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.AddRange([Guid.NewGuid(), Guid.NewGuid()]);
        var writer = new FakeNotificationWriter();

        var scanner = CreateScanner(queryRepository, alertRecordRepository, permissionedUserQuery, writer);
        var result = await scanner.ScanAsync(Noon);

        Assert.Equal(Permissions.InventoryView, permissionedUserQuery.LastPermissionKey);
        Assert.Equal(3, result.SignalCount);
        Assert.Equal(1, result.LowStockCount);
        Assert.Equal(1, result.ExpiringBatchCount);
        Assert.Equal(1, result.ExpiredBatchCount);
        Assert.Equal(6, result.MessageCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, result.RecipientCount);
        Assert.False(result.Truncated);
        Assert.Equal(6, writer.Written.Count);
        Assert.Equal(3, alertRecordRepository.Count);
        Assert.Equal(StockAlertFieldConstraints.MaxSignalsPerScan, queryRepository.LastLowStockMaxCount);
    }

    [Fact]
    public async Task 扫描_同日二次_应全部跳过且不新增消息()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        queryRepository.LowStock.Add(LowStockSignal());
        queryRepository.Expiring.Add(BatchSignal(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero)));
        var alertRecordRepository = new FakeAlertRecordRepository();
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.Add(Guid.NewGuid());
        var writer = new FakeNotificationWriter();
        var scanner = CreateScanner(queryRepository, alertRecordRepository, permissionedUserQuery, writer);

        await scanner.ScanAsync(Noon);
        var second = await scanner.ScanAsync(Noon.AddHours(1));

        Assert.Equal(2, second.SignalCount);
        Assert.Equal(0, second.MessageCount);
        Assert.Equal(2, second.SkippedCount);
        Assert.Equal(2, writer.Written.Count);
        Assert.Equal(2, alertRecordRepository.Count);
    }

    [Fact]
    public async Task 扫描_跨天_应可再次告警()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        queryRepository.LowStock.Add(LowStockSignal());
        var alertRecordRepository = new FakeAlertRecordRepository();
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.Add(Guid.NewGuid());
        var writer = new FakeNotificationWriter();
        var scanner = CreateScanner(queryRepository, alertRecordRepository, permissionedUserQuery, writer);

        await scanner.ScanAsync(Noon);
        var nextDay = await scanner.ScanAsync(Noon.AddDays(1));

        Assert.Equal(1, nextDay.MessageCount);
        Assert.Equal(0, nextDay.SkippedCount);
        Assert.Equal(2, writer.Written.Count);
        Assert.Equal(2, alertRecordRepository.Count);
    }

    [Fact]
    public async Task 扫描_无接收人_应不生成消息且不写台账()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        queryRepository.LowStock.Add(LowStockSignal());
        var alertRecordRepository = new FakeAlertRecordRepository();
        var writer = new FakeNotificationWriter();
        var scanner = CreateScanner(queryRepository, alertRecordRepository, new FakePermissionedUserQuery(), writer);

        var result = await scanner.ScanAsync(Noon);

        Assert.Equal(0, result.RecipientCount);
        Assert.Equal(0, result.MessageCount);
        Assert.Equal(0, result.SignalCount);
        Assert.Empty(writer.Written);
        Assert.Equal(0, alertRecordRepository.Count);
    }

    [Fact]
    public async Task 扫描_信号数超上限_应只处理前五百条并标记截断()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        for (var i = 0; i < 300; i++)
        {
            queryRepository.LowStock.Add(LowStockSignal());
        }

        for (var i = 0; i < 200; i++)
        {
            queryRepository.Expiring.Add(BatchSignal(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero)));
        }

        for (var i = 0; i < 100; i++)
        {
            queryRepository.Expired.Add(BatchSignal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        }

        var alertRecordRepository = new FakeAlertRecordRepository();
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.Add(Guid.NewGuid());
        var writer = new FakeNotificationWriter();
        var scanner = CreateScanner(queryRepository, alertRecordRepository, permissionedUserQuery, writer);

        var result = await scanner.ScanAsync(Noon);

        // 信号总数 600 超上限 → 按「低库存 → 近效期 → 过期」顺序只处理前 500 条
        Assert.Equal(600, result.SignalCount);
        Assert.True(result.Truncated);
        Assert.Equal(300, result.LowStockCount);
        Assert.Equal(200, result.ExpiringBatchCount);
        Assert.Equal(0, result.ExpiredBatchCount);
        Assert.Equal(StockAlertFieldConstraints.MaxSignalsPerScan, result.MessageCount);
        Assert.Equal(StockAlertFieldConstraints.MaxSignalsPerScan, alertRecordRepository.Count);
    }

    [Fact]
    public async Task 扫描_应按类型生成模板文案与跳转目标()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        var lowStockProductId = Guid.NewGuid();
        var lowStockWarehouseId = Guid.NewGuid();
        queryRepository.LowStock.Add(new StockAlertSignal
        {
            ProductId = lowStockProductId,
            ProductCode = "SKU-001",
            ProductName = "商品一",
            WarehouseId = lowStockWarehouseId,
            WarehouseName = "上海仓",
            Quantity = 10,
            SafetyStock = 100,
        });
        queryRepository.Expiring.Add(BatchSignal(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero)));
        queryRepository.Expired.Add(BatchSignal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.Add(Guid.NewGuid());
        var writer = new FakeNotificationWriter();
        var scanner = CreateScanner(queryRepository, new FakeAlertRecordRepository(), permissionedUserQuery, writer);

        await scanner.ScanAsync(Noon);

        var lowStock = Assert.Single(writer.Written, n => n.Type == NotificationType.LowStock);
        Assert.Equal("库存不足提醒", lowStock.Title);
        Assert.Equal("上海仓 商品一（SKU-001）当前库存 10，低于安全库存 100", lowStock.Content);
        Assert.Equal("inventory", lowStock.LinkRouteName);
        Assert.Contains(lowStockWarehouseId.ToString(), lowStock.LinkQuery);
        Assert.Contains("SKU-001", lowStock.LinkQuery);
        Assert.Equal($"product:{lowStockProductId}:warehouse:{lowStockWarehouseId}", lowStock.ResourceKey);

        var expiring = Assert.Single(writer.Written, n => n.Type == NotificationType.ExpiringBatch);
        Assert.Equal("批次近效期提醒", expiring.Title);
        Assert.Contains("2026-10-16", expiring.Content);
        Assert.Contains("剩 10 天", expiring.Content);
        Assert.Equal("batches", expiring.LinkRouteName);

        var expired = Assert.Single(writer.Written, n => n.Type == NotificationType.ExpiredBatch);
        Assert.Equal("批次已过期提醒", expired.Title);
        Assert.Contains("2026-09-01", expired.Content);
        Assert.Contains("请及时处理", expired.Content);
    }

    [Fact]
    public async Task 扫描_台账已存在同日记录_应跳过()
    {
        var queryRepository = new FakeStockAlertQueryRepository();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        queryRepository.LowStock.Add(new StockAlertSignal
        {
            ProductId = productId,
            ProductCode = "SKU-001",
            ProductName = "商品一",
            WarehouseId = warehouseId,
            WarehouseName = "上海仓",
            Quantity = 1,
            SafetyStock = 10,
        });
        var alertRecordRepository = new FakeAlertRecordRepository();
        alertRecordRepository.Seed(AlertType.LowStock, $"product:{productId}:warehouse:{warehouseId}", Today);
        var permissionedUserQuery = new FakePermissionedUserQuery();
        permissionedUserQuery.UserIds.Add(Guid.NewGuid());
        var writer = new FakeNotificationWriter();
        var scanner = CreateScanner(queryRepository, alertRecordRepository, permissionedUserQuery, writer);

        var result = await scanner.ScanAsync(Noon);

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.MessageCount);
        Assert.Empty(writer.Written);
    }
}
