using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Warehouses.UpdateWarehouseStatus;

/// <summary>
/// 仓库停用 / 启用请求格式校验：status 取值合法性
/// </summary>
public sealed class UpdateWarehouseStatusRequestValidator : AbstractValidator<UpdateWarehouseStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateWarehouseStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("仓库 id 不能为空");
        RuleFor(x => x.Status)
            .Must(s => s is (int)PartnerStatus.Enabled or (int)PartnerStatus.Disabled)
            .WithMessage("仓库状态值无效");
    }
}
