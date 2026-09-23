using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Warehouses.SetDefaultWarehouse;

/// <summary>
/// 设为默认仓用例：校验存在 → 停用仓不可设为默认（40123）→ 同一事务内清空其他仓默认标记并置当前仓为默认。
/// 默认仓是「不传仓库」开单的兜底，必须始终可用。
/// </summary>
public sealed class SetDefaultWarehouseRequestHandler
    : IRequestHandler<SetDefaultWarehouseRequest, WarehouseDto>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化设为默认仓用例处理器
    /// </summary>
    public SetDefaultWarehouseRequestHandler(
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理设为默认仓请求
    /// </summary>
    /// <param name="request">设为默认仓请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<WarehouseDto> HandleAsync(
        SetDefaultWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (warehouse is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "仓库不存在");
        }

        if (warehouse.Status != PartnerStatus.Enabled)
        {
            throw new BusinessException(ErrorCode.WarehouseDisabled, "仓库已停用，不可设为默认仓");
        }

        if (warehouse.IsDefault)
        {
            // 已是默认仓：幂等返回，不产生多余的清空 / 写入
            return WarehouseDtoMapper.ToWarehouseDto(warehouse);
        }

        var now = DateTimeOffset.UtcNow;

        // 清空其他仓默认标记与置当前仓为默认在同一事务内完成（保证「全局唯一」不出现中间态）
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _warehouseRepository.ClearDefaultAsync(warehouse.Id, cancellationToken);
            warehouse.IsDefault = true;
            warehouse.UpdatedAt = now;
            warehouse.UpdatedBy = _currentUser.UserId();
            await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("isDefault", "默认仓", null, "是");
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Warehouse,
                Action = AuditAction.Update,
                ResourceId = warehouse.Id,
                ResourceNo = warehouse.Name,
                Summary = $"设为默认仓 {warehouse.Name}",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return WarehouseDtoMapper.ToWarehouseDto(warehouse);
    }
}
