namespace App.Core.Entities;

/// <summary>
/// 商品分类实体（对应 PostgreSQL 表 Categories）。
/// 单级字典：无停用状态、无软删除；被商品引用的分类不可删除（改为编辑改名）。
/// </summary>
public sealed class Category
{
    /// <summary>分类 ID</summary>
    public Guid Id { get; set; }

    /// <summary>分类名称，唯一（大小写不敏感，由应用层判定）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
