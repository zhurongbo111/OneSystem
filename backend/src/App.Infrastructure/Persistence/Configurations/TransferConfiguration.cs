using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 调拨单表映射配置（表名 Transfers，单号唯一索引；外键不级联删除，业务对象只作废不删除）
/// </summary>
internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers");
        builder.HasKey(t => t.Id);

        // 长度取自 OrderFieldConstraints（单据域单一来源），与各 RequestValidator 保持一致
        builder.Property(t => t.TransferNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        // 转出 / 转入仓名称快照（038；长度与仓库名称同源）
        builder.Property(t => t.FromWarehouseName)
            .HasMaxLength(WarehouseFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(t => t.ToWarehouseName)
            .HasMaxLength(WarehouseFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(t => t.TransferDate).IsRequired();
        builder.Property(t => t.ItemCount).IsRequired();
        builder.Property(t => t.TotalQuantity).IsRequired();
        builder.Property(t => t.Status).HasConversion<short>().IsRequired();
        builder.Property(t => t.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，见 design.md §3.1）
        builder.HasIndex(t => t.TransferNo).IsUnique();
    }
}
