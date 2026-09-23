using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Warehouses.UpdateWarehouse;

/// <summary>
/// 编辑仓库用例：校验存在 → 名称唯一（排除自身）→ 更新名称 / 地址 / 联系人 / 电话 / 备注（编码不可改）。
/// 停用仓也允许编辑（修改信息后仍可停用）。
/// </summary>
public sealed class UpdateWarehouseRequestHandler : IRequestHandler<UpdateWarehouseRequest, WarehouseDto>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑仓库用例处理器
    /// </summary>
    public UpdateWarehouseRequestHandler(
        IWarehouseRepository warehouseRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _warehouseRepository = warehouseRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑仓库请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<WarehouseDto> HandleAsync(
        UpdateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (warehouse is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "仓库不存在");
        }

        var name = request.Name.Trim();

        // 查库约束：名称唯一（排除自身）
        if (await _warehouseRepository.ExistsByNameAsync(name, warehouse.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.WarehouseNameExists, "仓库名称已存在");
        }

        var beforeName = warehouse.Name;
        var beforeAddress = warehouse.Address;
        var beforeContact = warehouse.Contact;
        var beforePhone = warehouse.Phone;
        var beforeRemark = warehouse.Remark;
        var now = DateTimeOffset.UtcNow;

        // 编码不可修改，保持原值不变；可选字段全量覆盖（空 / 缺省 = 清空，AGENTS.md §4.5）
        warehouse.Name = name;
        warehouse.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        warehouse.Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim();
        warehouse.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        warehouse.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        warehouse.UpdatedAt = now;
        warehouse.UpdatedBy = _currentUser.UserId();

        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("name", "仓库名称", beforeName, warehouse.Name)
            .Add("address", "地址", beforeAddress, warehouse.Address)
            .Add("contact", "联系人", beforeContact, warehouse.Contact)
            .Add("phone", "联系电话", beforePhone, warehouse.Phone)
            .Add("remark", "备注", beforeRemark, warehouse.Remark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Warehouse,
            Action = AuditAction.Update,
            ResourceId = warehouse.Id,
            ResourceNo = warehouse.Name,
            Summary = $"编辑仓库 {warehouse.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return WarehouseDtoMapper.ToWarehouseDto(warehouse);
    }
}
