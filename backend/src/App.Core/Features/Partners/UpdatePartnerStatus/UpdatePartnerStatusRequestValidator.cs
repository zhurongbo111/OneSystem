using App.Core.Entities;
using FluentValidation;

namespace App.Core.Features.Partners.UpdatePartnerStatus;

/// <summary>
/// 往来单位停用 / 启用请求格式校验：status 取值合法性
/// </summary>
public sealed class UpdatePartnerStatusRequestValidator : AbstractValidator<UpdatePartnerStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePartnerStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("往来单位 id 不能为空");
        RuleFor(x => x.Status)
            .Must(s => s is (int)PartnerStatus.Enabled or (int)PartnerStatus.Disabled)
            .WithMessage("单位状态值无效");
    }
}
