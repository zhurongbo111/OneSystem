using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Roles.GetRoles;

/// <summary>
/// 角色分页列表请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetRolesRequestValidator : AbstractValidator<GetRolesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetRolesRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 关键词按角色名 / 备注匹配，长度上限取列长最大值（后端规则 §5.3 ③）
        RuleFor(x => x.Keyword)
            .MaximumLength(RoleFieldConstraints.RemarkMaxLength)
            .WithMessage($"关键词长度不能超过 {RoleFieldConstraints.RemarkMaxLength}");
    }
}
