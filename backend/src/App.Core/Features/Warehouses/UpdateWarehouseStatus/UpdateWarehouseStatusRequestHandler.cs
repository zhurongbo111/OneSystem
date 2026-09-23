using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Warehouses.UpdateWarehouseStatus;

/// <summary>
/// 仓库停用 / 启用用例：校验存在 → 默认仓不可停用（40124）→ 更新状态。
/// 停用仓不可被新单据选择，但保留库存 / 流水 / 历史单据引用；启用后可再次被选择。
/// </summary>
public sealed class UpdateWarehouseStatusRequestHandler
    : IRequestHandler<UpdateWarehouseStatusRequest, WarehouseDto>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化仓库停用 / 启用用例处理器
    /// </summary>
    public UpdateWarehouseStatusRequestHandler(
        IWarehouseRepository warehouseRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _warehouseRepository = warehouseRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理仓库停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<WarehouseDto> HandleAsync(
        UpdateWarehouseStatusRequest request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (warehouse is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "仓库不存在");
        }

        var target = request.Status == (int)PartnerStatus.Disabled
            ? PartnerStatus.Disabled
            : PartnerStatus.Enabled;

        // 默认仓必须始终可用（否则「不传仓库」的开单会失败）
        if (warehouse.IsDefault && target == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.WarehouseDefaultImmutable, "默认仓不可停用");
        }

        var beforeStatus = warehouse.Status;
        var now = DateTimeOffset.UtcNow;

        warehouse.Status = target;
        warehouse.UpdatedAt = now;
        warehouse.UpdatedBy = _currentUser.UserId();

        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("status", "状态", AuditText.PartnerStatus(beforeStatus), AuditText.PartnerStatus(warehouse.Status));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Warehouse,
            Action = AuditAction.StatusChange,
            ResourceId = warehouse.Id,
            ResourceNo = warehouse.Name,
            Summary = $"{(warehouse.Status == PartnerStatus.Enabled ? "启用" : "停用")}仓库 {warehouse.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return WarehouseDtoMapper.ToWarehouseDto(warehouse);
    }
}
