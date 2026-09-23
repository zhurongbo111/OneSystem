using App.Core.Entities;

namespace App.Core.Features.Warehouses;

/// <summary>
/// 仓库实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class WarehouseDtoMapper
{
    /// <summary>映射出参（列表 / 详情 / 新增 / 编辑 / 启停 / 设为默认共用同一模型）</summary>
    public static WarehouseDto ToWarehouseDto(Warehouse warehouse)
        => new()
        {
            Id = warehouse.Id.ToString(),
            Code = warehouse.Code,
            Name = warehouse.Name,
            Address = warehouse.Address,
            Contact = warehouse.Contact,
            Phone = warehouse.Phone,
            IsDefault = warehouse.IsDefault,
            Status = (int)warehouse.Status,
            Remark = warehouse.Remark,
            CreatedAt = warehouse.CreatedAt,
            UpdatedAt = warehouse.UpdatedAt,
        };

    /// <summary>映射下拉项（开单页仓库选择）</summary>
    public static WarehousePickDto ToWarehousePickDto(Warehouse warehouse)
        => new()
        {
            Id = warehouse.Id.ToString(),
            Code = warehouse.Code,
            Name = warehouse.Name,
            IsDefault = warehouse.IsDefault,
        };
}
