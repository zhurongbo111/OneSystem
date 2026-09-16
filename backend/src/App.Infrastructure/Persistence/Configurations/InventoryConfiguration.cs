using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 库存台账表映射配置（表名 Inventory，ProductId 唯一索引 / 外键不级联）
/// </summary>
internal sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventory");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        // 与商品一对一（单仓库；未来多仓库加 WarehouseId 并把唯一约束改为 (WarehouseId, ProductId)）
        builder.HasIndex(i => i.ProductId).IsUnique();

        // 外键不级联删除：商品停用不删库存行
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
