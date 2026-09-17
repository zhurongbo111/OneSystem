using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 销售退货单表映射配置（表名 SalesReturns，单号唯一索引；外键不级联删除，业务对象只作废不删除）
/// </summary>
internal sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns");
        builder.HasKey(r => r.Id);

        // 长度取自 OrderFieldConstraints（单据域单一来源），与各 RequestValidator 保持一致
        builder.Property(r => r.ReturnNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(r => r.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(r => r.ReturnDate).IsRequired();
        builder.Property(r => r.TotalAmount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(r => r.SettledAmount).IsRequired().HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        builder.Property(r => r.Status).HasConversion<short>().IsRequired();
        builder.Property(r => r.Remark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，见 specs/021-erp-purchase-return/design.md §3.1）
        builder.HasIndex(r => r.ReturnNo).IsUnique();
    }
}
