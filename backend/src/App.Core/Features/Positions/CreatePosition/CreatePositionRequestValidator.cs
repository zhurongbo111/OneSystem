using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Positions.CreatePosition;

/// <summary>
/// 新增岗位请求格式校验：只做数据格式检查；编码 / 名称是否重复等查库约束在 Handler 内
/// </summary>
public sealed class CreatePositionRequestValidator : AbstractValidator<CreatePositionRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreatePositionRequestValidator()
    {
        // 长度统一取自 PositionFieldConstraints（与 EF 配置一致，禁止硬编码）
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
