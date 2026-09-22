using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Accounts;

/// <summary>
/// 会计科目出参映射（集中一处，避免各用例重复拼装）。
/// 树节点映射为递归映射（子节点随父节点一并转换）。
/// </summary>
internal static class AccountDtoMapper
{
    /// <summary>科目实体 → 科目详情出参</summary>
    public static AccountDetailDto ToAccountDetailDto(Account account)
        => new()
        {
            Id = account.Id.ToString(),
            Code = account.Code,
            Name = account.Name,
            Category = (int)account.Category,
            Direction = (int)account.Direction,
            ParentId = account.ParentId?.ToString(),
            SortOrder = account.SortOrder,
            IsPreset = account.IsPreset,
            Status = (int)account.Status,
            Remark = account.Remark,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
        };

    /// <summary>
    /// 上级科目的展示文本（编码 + 名称，人读友好）；无上级输出 <c>null</c>
    /// （审计差异的空态占位由 <c>AuditSummary</c> 统一处理）
    /// </summary>
    /// <param name="parent">上级科目实体，可空</param>
    public static string? ParentLabel(Account? parent)
        => parent is null ? null : $"{parent.Code} {parent.Name}";

    /// <summary>科目树节点读模型 → 科目树节点出参（递归转换子节点）</summary>
    public static AccountTreeNodeDto ToAccountTreeNodeDto(AccountTreeNode node)
        => new()
        {
            Id = node.Id.ToString(),
            Code = node.Code,
            Name = node.Name,
            Category = (int)node.Category,
            Direction = (int)node.Direction,
            ParentId = node.ParentId?.ToString(),
            SortOrder = node.SortOrder,
            IsPreset = node.IsPreset,
            Status = (int)node.Status,
            Remark = node.Remark,
            Children = node.Children.Select(ToAccountTreeNodeDto).ToList(),
        };
}