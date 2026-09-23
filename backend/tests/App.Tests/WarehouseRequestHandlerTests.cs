using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Warehouses.CreateWarehouse;
using App.Core.Features.Warehouses.GetWarehouseById;
using App.Core.Features.Warehouses.GetWarehousePickList;
using App.Core.Features.Warehouses.GetWarehouses;
using App.Core.Features.Warehouses.SetDefaultWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouseStatus;

namespace App.Tests;

/// <summary>
/// 仓库用例测试（specs/038-erp-multi-warehouse tasks.md §5.1）：
/// CRUD / 编码与名称重复 / 默认仓不可停用 / 停用仓不可设默认 / 默认仓置顶 / pick 仅启用仓。
/// </summary>
public class WarehouseRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);

    private static Warehouse NewWarehouse(
        string code,
        string name,
        bool isDefault = false,
        PartnerStatus status = PartnerStatus.Enabled)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            IsDefault = isDefault,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static CreateWarehouseRequestHandler CreateCreateHandler(
        FakeWarehouseRepository warehouses, RecordingAuditLogger? audit = null)
        => new(warehouses, new StubCurrentUser(Guid.NewGuid()), audit ?? new RecordingAuditLogger());

    private static CreateWarehouseRequest NewCreateRequest(
        string code = "WH01", string name = "主仓", string? phone = null)
        => new() { Code = code, Name = name, Phone = phone };

    // ============================== 新增 ==============================

    [Fact]
    public async Task 新增仓库_成功_应为非默认仓且启用()
    {
        var warehouses = new FakeWarehouseRepository();
        var audit = new RecordingAuditLogger();
        var handler = CreateCreateHandler(warehouses, audit);

        var result = await handler.HandleAsync(NewCreateRequest());

        Assert.Equal("WH01", result.Code);
        Assert.Equal("主仓", result.Name);
        Assert.False(result.IsDefault);
        Assert.Equal((int)PartnerStatus.Enabled, result.Status);
        Assert.Equal(2, warehouses.Warehouses.Count); // 预置默认仓 + 本次新增
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Warehouse, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
    }

    [Fact]
    public async Task 新增仓库_编码重复_应报40122()
    {
        var warehouses = new FakeWarehouseRepository();
        warehouses.Seed(NewWarehouse("WH01", "主仓", isDefault: true));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateCreateHandler(warehouses).HandleAsync(NewCreateRequest(code: "wh01")));

        Assert.Equal(ErrorCode.WarehouseCodeExists, ex.Code);
    }

    [Fact]
    public async Task 新增仓库_名称重复_应报40125()
    {
        var warehouses = new FakeWarehouseRepository();
        warehouses.Seed(NewWarehouse("WH01", "主仓", isDefault: true));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateCreateHandler(warehouses).HandleAsync(NewCreateRequest(code: "WH02", name: "主仓")));

        Assert.Equal(ErrorCode.WarehouseNameExists, ex.Code);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task 编辑仓库_成功_编码保持原值()
    {
        var warehouses = new FakeWarehouseRepository();
        var warehouse = NewWarehouse("WH01", "主仓");
        warehouses.Seed(warehouse);

        var result = await new UpdateWarehouseRequestHandler(
                warehouses, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
            .HandleAsync(new UpdateWarehouseRequest { Id = warehouse.Id, Name = "主仓（改）", Address = "上海市" });

        Assert.Equal("WH01", result.Code);
        Assert.Equal("主仓（改）", result.Name);
        Assert.Equal("上海市", result.Address);
    }

    [Fact]
    public async Task 编辑仓库_名称与他仓重复_应报40125()
    {
        var warehouses = new FakeWarehouseRepository();
        var target = NewWarehouse("WH01", "主仓");
        warehouses.Seed(target);
        warehouses.Seed(NewWarehouse("WH02", "分仓"));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new UpdateWarehouseRequestHandler(warehouses, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
                .HandleAsync(new UpdateWarehouseRequest { Id = target.Id, Name = "分仓" }));

        Assert.Equal(ErrorCode.WarehouseNameExists, ex.Code);
    }

    [Fact]
    public async Task 编辑仓库_不存在_应报40400()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new UpdateWarehouseRequestHandler(
                    new FakeWarehouseRepository(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
                .HandleAsync(new UpdateWarehouseRequest { Id = Guid.NewGuid(), Name = "不存在" }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 启停 / 设为默认 ==============================

    [Fact]
    public async Task 停用仓库_成功()
    {
        var warehouses = new FakeWarehouseRepository();
        var warehouse = NewWarehouse("WH01", "主仓");
        warehouses.Seed(warehouse);

        var result = await new UpdateWarehouseStatusRequestHandler(
                warehouses, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
            .HandleAsync(new UpdateWarehouseStatusRequest { Id = warehouse.Id, Status = (int)PartnerStatus.Disabled });

        Assert.Equal((int)PartnerStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task 停用默认仓_应报40124()
    {
        var warehouses = new FakeWarehouseRepository();
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new UpdateWarehouseStatusRequestHandler(
                    warehouses, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
                .HandleAsync(new UpdateWarehouseStatusRequest
                {
                    Id = TestWarehouse.DefaultId,
                    Status = (int)PartnerStatus.Disabled,
                }));

        Assert.Equal(ErrorCode.WarehouseDefaultImmutable, ex.Code);
    }

    [Fact]
    public async Task 设为默认仓_应清空他仓默认标记()
    {
        var warehouses = new FakeWarehouseRepository();
        var warehouse = NewWarehouse("WH02", "分仓");
        warehouses.Seed(warehouse);

        var result = await new SetDefaultWarehouseRequestHandler(
                warehouses, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
            .HandleAsync(new SetDefaultWarehouseRequest { Id = warehouse.Id });

        Assert.True(result.IsDefault);
        Assert.False((await warehouses.GetByIdAsync(TestWarehouse.DefaultId))!.IsDefault);
        Assert.Single(warehouses.Warehouses.Where(w => w.IsDefault));
    }

    [Fact]
    public async Task 设为默认仓_停用仓_应报40123()
    {
        var warehouses = new FakeWarehouseRepository();
        var warehouse = NewWarehouse("WH02", "分仓", status: PartnerStatus.Disabled);
        warehouses.Seed(warehouse);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new SetDefaultWarehouseRequestHandler(
                    warehouses, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger())
                .HandleAsync(new SetDefaultWarehouseRequest { Id = warehouse.Id }));

        Assert.Equal(ErrorCode.WarehouseDisabled, ex.Code);
    }

    [Fact]
    public async Task 设为默认仓_已是默认仓_应幂等且不动其他仓()
    {
        var warehouses = new FakeWarehouseRepository();
        var calls = new List<string>();
        var handler = new SetDefaultWarehouseRequestHandler(
            warehouses, new RecordingUnitOfWork(calls), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var result = await handler.HandleAsync(new SetDefaultWarehouseRequest { Id = TestWarehouse.DefaultId });

        Assert.True(result.IsDefault);
        Assert.Empty(calls); // 无需开事务 / 提交
    }

    // ============================== 查询 / 下拉 ==============================

    [Fact]
    public async Task 分页查询_默认仓置顶_筛选条件透传()
    {
        var warehouses = new FakeWarehouseRepository();
        warehouses.Seed(NewWarehouse("WH02", "A 分仓", status: PartnerStatus.Disabled));

        var result = await new GetWarehousesRequestHandler(warehouses)
            .HandleAsync(new GetWarehousesRequest { Page = 1, PageSize = 20, Keyword = "仓" });

        Assert.Equal(2, result.Total);
        Assert.Equal(TestWarehouse.DefaultId.ToString(), result.Items[0].Id); // 默认仓置顶
    }

    [Fact]
    public async Task 分页查询_按状态筛选()
    {
        var warehouses = new FakeWarehouseRepository();
        warehouses.Seed(NewWarehouse("WH02", "分仓", status: PartnerStatus.Disabled));

        var result = await new GetWarehousesRequestHandler(warehouses)
            .HandleAsync(new GetWarehousesRequest { Page = 1, PageSize = 20, Status = PartnerStatus.Disabled });

        var item = Assert.Single(result.Items);
        Assert.Equal("WH02", item.Code);
    }

    [Fact]
    public async Task 详情查询_不存在_应报40400()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new GetWarehouseByIdRequestHandler(new FakeWarehouseRepository())
                .HandleAsync(new GetWarehouseByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 下拉查询_仅返回启用仓()
    {
        var warehouses = new FakeWarehouseRepository();
        warehouses.Seed(NewWarehouse("WH02", "启用分仓"));
        warehouses.Seed(NewWarehouse("WH03", "停用分仓", status: PartnerStatus.Disabled));

        var result = await new GetWarehousePickListRequestHandler(warehouses)
            .HandleAsync(new GetWarehousePickListRequest());

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, w => w.Code == "WH03");
        Assert.Contains(result, w => w.Code == "DEFAULT" && w.IsDefault);
    }
}
