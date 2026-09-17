using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 收付款单核销明细表映射配置（表名 SettlementItems，外键不级联删除，见 design.md §2.2）。
/// 单据号 / 单据日期 / 单据总额为快照字段；被核销单据 id 建索引（作废回退按单据反查）。
/// </summary>
internal sealed class SettlementItemConfiguration : IEntityTypeConfiguration<SettlementItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SettlementItem> builder)
    {
        builder.ToTable("SettlementItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderType).HasConversion<short>().IsRequired();
        builder.Property(i => i.OrderNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(i => i.OrderDate).IsRequired();
        builder.Property(i => i.OrderTotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(i => i.Amount).IsRequired().HasColumnType("numeric(18,2)");

        builder.HasIndex(i => i.SettlementId);
        builder.HasIndex(i => i.OrderId);

        // 主表外键：不级联删除（收付款单只作废不删除）
        builder.HasOne<Settlement>()
            .WithMany()
            .HasForeignKey(i => i.SettlementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
