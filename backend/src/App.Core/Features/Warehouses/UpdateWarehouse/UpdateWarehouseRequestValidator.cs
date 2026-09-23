using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Warehouses.UpdateWarehouse;

/// <summary>
/// 编辑仓库请求格式校验：与创建同源（去掉不可改的编码），长度 / 取值边界取自 WarehouseFieldConstraints
/// </summary>
public sealed class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("仓库 id 不能为空");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("仓库名称不能为空")
            .Length(WarehouseFieldConstraints.NameMinLength, WarehouseFieldConstraints.NameMaxLength)
            .WithMessage($"仓库名称长度必须在 {WarehouseFieldConstraints.NameMinLength} 到 {WarehouseFieldConstraints.NameMaxLength} 之间");

        RuleFor(x => x.Address)
            .MaximumLength(WarehouseFieldConstraints.AddressMaxLength)
            .WithMessage($"地址长度不能超过 {WarehouseFieldConstraints.AddressMaxLength} 个字符")
            .When(x => x.Address is not null);

        RuleFor(x => x.Contact)
            .MaximumLength(WarehouseFieldConstraints.ContactMaxLength)
            .WithMessage($"联系人长度不能超过 {WarehouseFieldConstraints.ContactMaxLength} 个字符")
            .When(x => x.Contact is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(WarehouseFieldConstraints.PhoneMaxLength)
            .Matches(WarehouseFieldConstraints.PhonePattern).WithMessage("联系电话格式不正确")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Remark)
            .MaximumLength(WarehouseFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {WarehouseFieldConstraints.RemarkMaxLength} 个字符")
            .When(x => x.Remark is not null);
    }
}
