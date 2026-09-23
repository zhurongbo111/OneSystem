using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 库存变动流水表映射配置（表名 StockMovements；纯追加，无更新 / 删除）
/// </summary>
internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MovementType).HasConversion<short>().IsRequired();
        builder.Property(m => m.Quantity).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        // 成本列（erp-cost）：本次变动的单价与金额，精度 numeric(18,4)，默认 0（无索引需求）
        builder.Property(m => m.UnitCost)
            .IsRequired()
            .HasColumnType("numeric(18,4)");
        builder.Property(m => m.TotalCost)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        // 列长取自 OrderFieldConstraints（单一来源）：来源即单据单号，长度规则与单据域同源
        builder.Property(m => m.SourceNo).HasMaxLength(OrderFieldConstraints.OrderNoMaxLength);
        builder.Property(m => m.Remark).HasMaxLength(OrderFieldConstraints.RemarkMaxLength);

        // 按仓 + 商品下钻流水（038）+ 全局列表按变动时间倒序 + 按单号查询
        builder.HasIndex(m => new { m.WarehouseId, m.ProductId, m.CreatedAt });
        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });
        builder.HasIndex(m => m.CreatedAt).IsDescending();
        builder.HasIndex(m => m.SourceNo);

        // 商品 / 仓库停用不影响历史流水 → 外键禁止级联删除
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
