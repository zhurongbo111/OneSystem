using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 调拨单明细表映射配置（表名 TransferItems，TransferId 索引；外键不级联删除）
/// </summary>
internal sealed class TransferItemConfiguration : IEntityTypeConfiguration<TransferItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransferItem> builder)
    {
        builder.ToTable("TransferItems");
        builder.HasKey(i => i.Id);

        // 长度取自单一来源：ProductFieldConstraints（商品档案列，快照同源）
        builder.Property(i => i.ProductCode)
            .HasMaxLength(ProductFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(32)");
        builder.Property(i => i.ProductName)
            .HasMaxLength(ProductFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(i => i.Unit)
            .HasMaxLength(ProductFieldConstraints.UnitMaxLength)
            .IsRequired()
            .HasColumnType("varchar(10)");
        builder.Property(i => i.Quantity).IsRequired();

        builder.HasIndex(i => i.TransferId);
    }
}
