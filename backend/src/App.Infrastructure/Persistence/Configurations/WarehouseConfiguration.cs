using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 仓库表映射配置（表名 Warehouses，specs/038-erp-multi-warehouse/design.md §2.1）：
/// 编码 / 名称唯一索引，字段长度取自 <see cref="WarehouseFieldConstraints"/>（单一来源）。
/// </summary>
internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(w => w.Id);

        // 长度取自 WarehouseFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(w => w.Code)
            .HasMaxLength(WarehouseFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(w => w.Name)
            .HasMaxLength(WarehouseFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(w => w.Address)
            .HasMaxLength(WarehouseFieldConstraints.AddressMaxLength)
            .HasColumnType("varchar(100)");
        builder.Property(w => w.Contact)
            .HasMaxLength(WarehouseFieldConstraints.ContactMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(w => w.Phone)
            .HasMaxLength(WarehouseFieldConstraints.PhoneMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(w => w.Remark)
            .HasMaxLength(WarehouseFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");

        // 默认仓（全系统唯一，应用层保证）与状态（复用 PartnerStatus）
        builder.Property(w => w.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(w => w.Status).HasConversion<short>().IsRequired();
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();

        // 编码 / 名称唯一（大小写敏感；忽略大小写的判定由应用层负责，同往来单位原则）
        builder.HasIndex(w => w.Code).IsUnique();
        builder.HasIndex(w => w.Name).IsUnique();
    }
}
