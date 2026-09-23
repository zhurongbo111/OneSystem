using App.Core.Entities;
using App.Core.Features.Inventory.UpdateInventorySafetyStock;
using App.Core.Features.Warehouses.CreateWarehouse;
using App.Core.Features.Warehouses.GetWarehouses;
using App.Core.Features.Warehouses.UpdateWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouseStatus;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 仓库字段约束一致性测试（specs/038-erp-multi-warehouse tasks.md §5.7）：
/// EF 模型列长 / 唯一索引必须与 <see cref="WarehouseFieldConstraints"/> 常量同源，
/// 创建与编辑两处规则一致，查询关键词与仓级安全库存边界按常量取值。
/// </summary>
public class WarehouseFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    private static bool HasUniqueIndex<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == propertyName);

    [Fact]
    public void EF模型_Warehouses表列长与唯一索引_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(WarehouseFieldConstraints.CodeMaxLength, GetMaxLength<Warehouse>(dbContext, nameof(Warehouse.Code)));
        Assert.Equal(WarehouseFieldConstraints.NameMaxLength, GetMaxLength<Warehouse>(dbContext, nameof(Warehouse.Name)));
        Assert.Equal(WarehouseFieldConstraints.AddressMaxLength, GetMaxLength<Warehouse>(dbContext, nameof(Warehouse.Address)));
        Assert.Equal(WarehouseFieldConstraints.ContactMaxLength, GetMaxLength<Warehouse>(dbContext, nameof(Warehouse.Contact)));
        Assert.Equal(WarehouseFieldConstraints.PhoneMaxLength, GetMaxLength<Warehouse>(dbContext, nameof(Warehouse.Phone)));
        Assert.Equal(WarehouseFieldConstraints.RemarkMaxLength, GetMaxLength<Warehouse>(dbContext, nameof(Warehouse.Remark)));

        Assert.True(HasUniqueIndex<Warehouse>(dbContext, nameof(Warehouse.Code)));
        Assert.True(HasUniqueIndex<Warehouse>(dbContext, nameof(Warehouse.Name)));
    }

    [Fact]
    public void EF模型_库存唯一键_应为商品与仓库组合()
    {
        using var dbContext = TestSupport.CreateDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(Inventory))!;
        var uniqueIndex = entity.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(
            [nameof(Inventory.ProductId), nameof(Inventory.WarehouseId)],
            uniqueIndex.Properties.Select(p => p.Name).ToArray());
        Assert.True(entity.FindProperty(nameof(Inventory.WarehouseId))!.IsNullable == false);
        Assert.False(entity.FindProperty(nameof(Inventory.SafetyStock))!.IsNullable);
    }

    [Fact]
    public void 仓库编码_长度与格式_创建校验应生效()
    {
        var tooShort = new CreateWarehouseRequestValidator()
            .Validate(new CreateWarehouseRequest { Code = "A", Name = "名称" });
        Assert.False(tooShort.IsValid);

        var tooLong = new CreateWarehouseRequestValidator()
            .Validate(new CreateWarehouseRequest
            {
                Code = new string('a', WarehouseFieldConstraints.CodeMaxLength + 1),
                Name = "名称",
            });
        Assert.False(tooLong.IsValid);

        var invalidPattern = new CreateWarehouseRequestValidator()
            .Validate(new CreateWarehouseRequest { Code = "编码 01", Name = "名称" });
        Assert.False(invalidPattern.IsValid);

        var ok = new CreateWarehouseRequestValidator()
            .Validate(new CreateWarehouseRequest { Code = "WH-01", Name = "名称" });
        Assert.True(ok.IsValid);
    }

    [Fact]
    public void 仓库名称_区间_创建与编辑应一致()
    {
        var empty = new CreateWarehouseRequestValidator().Validate(new CreateWarehouseRequest { Code = "WH01" });
        var tooLong = new CreateWarehouseRequestValidator().Validate(new CreateWarehouseRequest
        {
            Code = "WH01",
            Name = new string('名', WarehouseFieldConstraints.NameMaxLength + 1),
        });
        Assert.False(empty.IsValid);
        Assert.False(tooLong.IsValid);

        var updateEmpty = new UpdateWarehouseRequestValidator().Validate(new UpdateWarehouseRequest { Id = Guid.NewGuid() });
        var updateTooLong = new UpdateWarehouseRequestValidator().Validate(new UpdateWarehouseRequest
        {
            Id = Guid.NewGuid(),
            Name = new string('名', WarehouseFieldConstraints.NameMaxLength + 1),
        });
        Assert.False(updateEmpty.IsValid);
        Assert.False(updateTooLong.IsValid);

        var ok = new UpdateWarehouseRequestValidator().Validate(new UpdateWarehouseRequest
        {
            Id = Guid.NewGuid(),
            Name = new string('名', WarehouseFieldConstraints.NameMaxLength),
        });
        Assert.True(ok.IsValid);
    }

    [Fact]
    public void 联系电话_格式_创建与编辑应一致()
    {
        var invalid = new CreateWarehouseRequestValidator()
            .Validate(new CreateWarehouseRequest { Code = "WH01", Name = "名称", Phone = "12345" });
        var invalidUpdate = new UpdateWarehouseRequestValidator()
            .Validate(new UpdateWarehouseRequest { Id = Guid.NewGuid(), Name = "名称", Phone = "12345" });
        Assert.False(invalid.IsValid);
        Assert.False(invalidUpdate.IsValid);

        var ok = new CreateWarehouseRequestValidator()
            .Validate(new CreateWarehouseRequest { Code = "WH01", Name = "名称", Phone = "13800138000" });
        Assert.True(ok.IsValid);
    }

    [Fact]
    public void 查询关键词_上限应等于被匹配列的最大列长()
    {
        var maxLength = WarehouseFieldConstraints.KeywordMaxLength;

        Assert.True(new GetWarehousesRequestValidator()
            .Validate(new GetWarehousesRequest { Keyword = new string('a', maxLength) }).IsValid);
        Assert.False(new GetWarehousesRequestValidator()
            .Validate(new GetWarehousesRequest { Keyword = new string('a', maxLength + 1) }).IsValid);
    }

    [Fact]
    public void 分页与状态_边界校验()
    {
        Assert.False(new GetWarehousesRequestValidator().Validate(new GetWarehousesRequest { Page = 0 }).IsValid);
        Assert.False(new GetWarehousesRequestValidator().Validate(new GetWarehousesRequest { PageSize = 101 }).IsValid);
        Assert.False(new GetWarehousesRequestValidator()
            .Validate(new GetWarehousesRequest { Status = (PartnerStatus)9 }).IsValid);

        Assert.False(new UpdateWarehouseStatusRequestValidator()
            .Validate(new UpdateWarehouseStatusRequest { Id = Guid.NewGuid(), Status = 9 }).IsValid);
        Assert.True(new UpdateWarehouseStatusRequestValidator()
            .Validate(new UpdateWarehouseStatusRequest { Id = Guid.NewGuid(), Status = (int)PartnerStatus.Disabled }).IsValid);
    }

    [Fact]
    public void 仓级安全库存_区间边界_应取常量()
    {
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        Assert.False(new UpdateInventorySafetyStockRequestValidator().Validate(new UpdateInventorySafetyStockRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            SafetyStock = WarehouseFieldConstraints.SafetyStockMinValue - 1,
        }).IsValid);
        Assert.False(new UpdateInventorySafetyStockRequestValidator().Validate(new UpdateInventorySafetyStockRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            SafetyStock = WarehouseFieldConstraints.SafetyStockMaxValue + 1,
        }).IsValid);
        Assert.True(new UpdateInventorySafetyStockRequestValidator().Validate(new UpdateInventorySafetyStockRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            SafetyStock = WarehouseFieldConstraints.SafetyStockMinValue,
        }).IsValid);
        Assert.True(new UpdateInventorySafetyStockRequestValidator().Validate(new UpdateInventorySafetyStockRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            SafetyStock = WarehouseFieldConstraints.SafetyStockMaxValue,
        }).IsValid);
    }
}
