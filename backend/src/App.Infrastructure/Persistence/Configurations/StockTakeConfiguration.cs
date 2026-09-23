using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 盘点 / 期初建账单据表映射配置（表名 StockTakes，单号唯一索引；外键不级联删除）。
/// 一步式单据无状态字段；长度取自 StockTakeFieldConstraints（单一来源，内部引用单据域常量）。
/// </summary>
internal sealed class StockTakeConfiguration : IEntityTypeConfiguration<StockTake>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StockTake> builder)
    {
        builder.ToTable("StockTakes");
        builder.HasKey(t => t.Id);

        // 长度取自 StockTakeFieldConstraints（单一来源，内部引用 OrderFieldConstraints）
        builder.Property(t => t.TakeNo)
            .HasMaxLength(StockTakeFieldConstraints.TakeNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(t => t.Type).HasConversion<short>().IsRequired();
        // 盘点仓名称快照（038；长度与仓库名称同源）
        builder.Property(t => t.WarehouseName)
            .HasMaxLength(WarehouseFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(t => t.TakeDate).IsRequired();
        builder.Property(t => t.ItemCount).IsRequired();
        builder.Property(t => t.DiffItemCount).IsRequired();
        builder.Property(t => t.Remark)
            .HasMaxLength(StockTakeFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，对齐采购 / 销售单，见 design.md §2.1 / erp-purchase §3.6）
        builder.HasIndex(t => t.TakeNo).IsUnique();
    }
}
