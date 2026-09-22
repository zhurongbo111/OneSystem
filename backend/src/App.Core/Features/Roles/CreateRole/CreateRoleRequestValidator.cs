using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Roles.CreateRole;

/// <summary>
/// 新增角色请求格式校验：只做数据格式检查；名称唯一性与权限点合法性在 Handler 内
/// </summary>
public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("角色名称不能为空")
            .Length(RoleFieldConstraints.NameMinLength, RoleFieldConstraints.NameMaxLength)
            .WithMessage($"角色名称长度必须在 {RoleFieldConstraints.NameMinLength} 到 {RoleFieldConstraints.NameMaxLength} 之间");

        RuleFor(x => x.Remark)
            .MaximumLength(RoleFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {RoleFieldConstraints.RemarkMaxLength}")
            .When(x => !string.IsNullOrWhiteSpace(x.Remark));

        // 单个角色的权限点数量上限（授权粒度边界，与 Permissions.All 规模同级）
        RuleFor(x => x.PermissionKeys)
            .NotEmpty().WithMessage("权限点不能为空，请至少选择一项")
            .Must(keys => keys.Count <= RoleFieldConstraints.PermissionsPerRoleMaxCount)
            .WithMessage($"权限点数量不能超过 {RoleFieldConstraints.PermissionsPerRoleMaxCount} 项");

        RuleForEach(x => x.PermissionKeys)
            .MaximumLength(RoleFieldConstraints.PermissionKeyMaxLength)
            .WithMessage($"权限点长度不能超过 {RoleFieldConstraints.PermissionKeyMaxLength}");
    }
}
