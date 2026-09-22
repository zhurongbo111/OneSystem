using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Positions.UpdatePosition;

/// <summary>
/// 编辑岗位请求格式校验：只做数据格式检查；唯一性等查库约束在 Handler 内
/// </summary>
public sealed class UpdatePositionRequestValidator : AbstractValidator<UpdatePositionRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePositionRequestValidator()
    {
        // 路由参数兜底：Controller 以路由值覆盖，避免请求体缺 id 时进入 Handler
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("岗位 id 不能为空");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("岗位编码不能为空")
            .MaximumLength(PositionFieldConstraints.CodeMaxLength)
            .WithMessage($"岗位编码长度不能超过 {PositionFieldConstraints.CodeMaxLength}");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("岗位名称不能为空")
            .MaximumLength(PositionFieldConstraints.NameMaxLength)
            .WithMessage($"岗位名称长度不能超过 {PositionFieldConstraints.NameMaxLength}");

        RuleFor(x => x.Status)
            .Must(status => status is (int)PositionStatus.Enabled or (int)PositionStatus.Disabled)
            .WithMessage("岗位状态值无效");

        RuleFor(x => x.Remark)
            .MaximumLength(PositionFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {PositionFieldConstraints.RemarkMaxLength}");
    }
}
