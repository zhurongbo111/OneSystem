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

        // 列长取自 OrderFieldConstraints（单一来源）：来源即单据单号，长度规则与单据域同源
        builder.Property(m => m.SourceNo).HasMaxLength(OrderFieldConstraints.OrderNoMaxLength);
        builder.Property(m => m.Remark).HasMaxLength(OrderFieldConstraints.RemarkMaxLength);

        // 按商品下钻流水 + 全局列表按变动时间倒序 + 按单号查询
        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });
        builder.HasIndex(m => m.CreatedAt).IsDescending();
        builder.HasIndex(m => m.SourceNo);

        // 商品停用不影响历史流水 → 外键禁止级联删除
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
