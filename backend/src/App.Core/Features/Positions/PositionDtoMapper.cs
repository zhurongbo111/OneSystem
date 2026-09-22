using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Positions;

/// <summary>
/// 岗位出参映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class PositionDtoMapper
{
    /// <summary>岗位列表读模型 → 列表项出参</summary>
    public static PositionListItemDto ToPositionListItemDto(PositionListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            Status = (int)item.Status,
            Remark = item.Remark,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };

    /// <summary>岗位实体 → 岗位详情出参</summary>
    public static PositionDetailDto ToPositionDetailDto(Position position)
        => new()
        {
            Id = position.Id.ToString(),
            Code = position.Code,
            Name = position.Name,
            Status = (int)position.Status,
            Remark = position.Remark,
            CreatedAt = position.CreatedAt,
            UpdatedAt = position.UpdatedAt,
        };

    /// <summary>岗位选择读模型 → 选择项出参</summary>
    public static PositionPickDto ToPositionPickDto(PositionPickItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
        };
}
