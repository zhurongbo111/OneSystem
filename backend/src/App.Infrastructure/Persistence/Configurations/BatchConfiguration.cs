using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 批次档案表映射配置（表名 Batches，specs/040-erp-batch-expiry/design.md §2.2；
/// 同商品内 BatchNo 唯一 / 外键不级联删除——批次不删除，保留历史引用）
/// </summary>
internal sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches");
        builder.HasKey(b => b.Id);

        // 长度取自 BatchFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(b => b.BatchNo)
            .HasMaxLength(BatchFieldConstraints.BatchNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(b => b.ProductionDate).IsRequired(false);
        builder.Property(b => b.ExpiryDate).IsRequired(false);
        builder.Property(b => b.Status).HasConversion<short>().IsRequired();
        builder.Property(b => b.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        // 同商品内批次号唯一（大小写敏感；忽略大小写的判定由应用层 ExistsByBatchNoAsync 负责）
        builder.HasIndex(b => new { b.ProductId, b.BatchNo }).IsUnique();

        // 近效期 / 过期筛选（040 §2.2）
        builder.HasIndex(b => b.ExpiryDate);

        // 外键不级联删除：批次不删除（历史单据与流水保留引用）
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
