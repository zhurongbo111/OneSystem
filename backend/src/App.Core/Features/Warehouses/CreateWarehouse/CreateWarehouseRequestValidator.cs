using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Warehouses.CreateWarehouse;

/// <summary>
/// 新增仓库请求格式校验：只做数据格式检查；编码 / 名称是否已存在等查库约束在 Handler 内。
/// 长度 / 取值边界统一取自 WarehouseFieldConstraints（与 EF 配置一致，禁止硬编码）。
/// </summary>
public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateWarehouseRequestValidator()
    {
        // 入参去首尾空白在 Handler 内统一处理，此处只做格式校验
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("仓库编码不能为空")
            .Length(WarehouseFieldConstraints.CodeMinLength, WarehouseFieldConstraints.CodeMaxLength)
            .WithMessage($"仓库编码长度必须在 {WarehouseFieldConstraints.CodeMinLength} 到 {WarehouseFieldConstraints.CodeMaxLength} 之间")
            .Matches(WarehouseFieldConstraints.CodePattern)
            .WithMessage("仓库编码只能包含字母、数字、下划线与连字符");

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
