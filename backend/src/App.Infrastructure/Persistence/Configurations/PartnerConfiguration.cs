using App.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 往来单位表映射配置（表名 Partners，字段长度 / 必填 / 名称唯一索引）
/// </summary>
internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("Partners");
        builder.HasKey(p => p.Id);

        // 长度取自 PartnerFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(p => p.Name)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(p => p.Type).HasConversion<short>().IsRequired();
        builder.Property(p => p.Contact)
            .HasMaxLength(PartnerFieldConstraints.ContactMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(p => p.Phone)
            .HasMaxLength(PartnerFieldConstraints.PhoneMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(p => p.Address)
            .HasMaxLength(PartnerFieldConstraints.AddressMaxLength)
            .HasColumnType("varchar(100)");
        builder.Property(p => p.Remark)
            .HasMaxLength(PartnerFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(p => p.Status).HasConversion<short>().IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // 名称唯一（大小写敏感；忽略大小写的判定由应用层负责，见 design.md 2.1）
        builder.HasIndex(p => p.Name).IsUnique();
    }
}
