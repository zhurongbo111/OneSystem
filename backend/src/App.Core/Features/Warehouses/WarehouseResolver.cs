using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Warehouses;

/// <summary>
/// 单据仓库解析（specs/038-erp-multi-warehouse/design.md §3.4 第 1 步，五类单据 / 盘点共用一处，避免逐域重复）：
/// <c>warehouseId</c> 为空 → 取默认仓（兼容存量调用方）；非空 → 取仓，不存在 <c>40400</c>、已停用 <c>40123</c>。
/// </summary>
internal static class WarehouseResolver
{
    /// <summary>
    /// 解析单据实际使用的仓库（不存在 / 停用即抛业务异常）
    /// </summary>
    /// <param name="warehouseRepository">仓库仓储</param>
    /// <param name="warehouseId">请求中的仓库 id（可空 = 默认仓）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<Warehouse> ResolveAsync(
        IWarehouseRepository warehouseRepository, Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        if (warehouseId is null)
        {
            var fallback = await warehouseRepository.GetDefaultAsync(cancellationToken);
            if (fallback is null)
            {
                // 默认仓由迁移内置且不可停用，缺失属数据异常
                throw new BusinessException(ErrorCode.Internal, "默认仓不存在，请检查仓库档案");
            }

            return fallback;
        }

        var warehouse = await warehouseRepository.GetByIdAsync(warehouseId.Value, cancellationToken);
        if (warehouse is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "仓库不存在");
        }

        if (warehouse.Status != PartnerStatus.Enabled)
        {
            throw new BusinessException(ErrorCode.WarehouseDisabled, $"仓库 {warehouse.Name} 已停用，不可用于开单");
        }

        return warehouse;
    }
}
