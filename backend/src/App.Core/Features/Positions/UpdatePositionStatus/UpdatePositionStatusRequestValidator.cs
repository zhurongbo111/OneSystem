using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Positions.UpdatePositionStatus;

/// <summary>
/// 岗位停用 / 启用请求格式校验：只做数据格式检查；岗位是否存在在 Handler 内
/// </summary>
public sealed class UpdatePositionStatusRequestValidator : AbstractValidator<UpdatePositionStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePositionStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is (int)PositionStatus.Enabled or (int)PositionStatus.Disabled)
            .WithMessage("岗位状态值无效");
    }
}
