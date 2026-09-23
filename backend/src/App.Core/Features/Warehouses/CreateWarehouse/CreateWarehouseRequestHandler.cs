using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Warehouses.CreateWarehouse;

/// <summary>
/// 新增仓库用例：校验编码 / 名称唯一（大小写不敏感）→ 落库（默认启用、非默认仓，设为默认另走专用接口）。
/// 单一仓储写由仓储自身持久化，无需 IUnitOfWork。
/// </summary>
public sealed class CreateWarehouseRequestHandler : IRequestHandler<CreateWarehouseRequest, WarehouseDto>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增仓库用例处理器
    /// </summary>
    public CreateWarehouseRequestHandler(
        IWarehouseRepository warehouseRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _warehouseRepository = warehouseRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增仓库请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<WarehouseDto> HandleAsync(
        CreateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码 / 名称唯一（大小写不敏感）
        if (await _warehouseRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.WarehouseCodeExists, "仓库编码已存在");
        }

        if (await _warehouseRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.WarehouseNameExists, "仓库名称已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            // 新增一律为非默认仓且启用；设为默认走 PUT /api/warehouses/{id}/default
            IsDefault = false,
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        await _warehouseRepository.AddAsync(warehouse, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("code", "仓库编码", null, warehouse.Code)
            .Add("name", "仓库名称", null, warehouse.Name)
            .Add("address", "地址", null, warehouse.Address)
            .Add("contact", "联系人", null, warehouse.Contact)
            .Add("phone", "联系电话", null, warehouse.Phone)
            .Add("remark", "备注", null, warehouse.Remark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Warehouse,
            Action = AuditAction.Create,
            ResourceId = warehouse.Id,
            ResourceNo = warehouse.Name,
            Summary = $"新增仓库 {warehouse.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return WarehouseDtoMapper.ToWarehouseDto(warehouse);
    }
}
