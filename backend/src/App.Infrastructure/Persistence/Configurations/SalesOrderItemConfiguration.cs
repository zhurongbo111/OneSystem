using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 销售订单明细表映射配置（表名 SalesOrderItems，OrderId 索引；外键不级联删除）
/// </summary>
internal sealed class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("SalesOrderItems");
        builder.HasKey(i => i.Id);

        // 长度取自单一来源：ProductFieldConstraints（商品档案列，快照同源）
        builder.Property(i => i.ProductName)
            .HasMaxLength(ProductFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(i => i.Unit)
            .HasMaxLength(ProductFieldConstraints.UnitMaxLength)
            .IsRequired()
            .HasColumnType("varchar(10)");
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.Subtotal).IsRequired().HasColumnType("numeric(18,2)");

        // 累计已发数量：新建订单恒为 0，由出入库单关联回写（design.md §2.2）
        builder.Property(i => i.FulfilledQuantity).IsRequired().HasDefaultValue(0);

        builder.HasIndex(i => i.OrderId);
    }
}
