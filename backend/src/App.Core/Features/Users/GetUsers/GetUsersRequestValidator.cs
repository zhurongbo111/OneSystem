using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Users.GetUsers;

/// <summary>
/// 用户分页列表请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetUsersRequestValidator : AbstractValidator<GetUsersRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetUsersRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Status)
            .Must(status => status is null or 0 or 1).WithMessage("状态只能是 0（禁用）或 1（启用）");

        // 关键词匹配用户名 / 显示名，长度上限取二者共同约束
        RuleFor(x => x.Keyword)
            .MaximumLength(UserFieldConstraints.UsernameMaxLength)
            .WithMessage($"关键词长度不能超过 {UserFieldConstraints.UsernameMaxLength}");
    }
}
