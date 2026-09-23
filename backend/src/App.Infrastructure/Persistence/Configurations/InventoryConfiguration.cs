using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 库存台账表映射配置（表名 Inventory，唯一键 <c>(ProductId, WarehouseId)</c> / 外键不级联；
/// 仓库维度见 specs/038-erp-multi-warehouse/design.md §2.2）
/// </summary>
internal sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventory");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).IsRequired();

        // 仓级安全库存（038）：判定唯一来源，默认 0（无阈值）
        builder.Property(i => i.SafetyStock).IsRequired().HasDefaultValue(0);
        builder.Property(i => i.UpdatedAt).IsRequired();

        // 成本列（erp-cost）：结存成本额与移动加权平均单价，精度 numeric(18,4)，默认 0
        builder.Property(i => i.CostAmount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");
        builder.Property(i => i.AverageCost)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        // 库存的唯一粒度 = 商品 × 仓库（038 维度升级；原「ProductId 唯一」单仓约束已删除）
        builder.HasIndex(i => new { i.ProductId, i.WarehouseId }).IsUnique();

        // 外键不级联删除：商品 / 仓库停用不删库存行
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(i => i.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
