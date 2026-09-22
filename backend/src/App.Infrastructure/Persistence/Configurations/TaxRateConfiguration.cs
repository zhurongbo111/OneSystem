using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 税率表映射配置（表名 TaxRates，编码与名称唯一索引 / 税率 numeric(9,4)）
/// （specs/031-erp-finance-master/design.md §2.2）。
/// </summary>
internal sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TaxRates");
        builder.HasKey(t => t.Id);

        // 长度 / 精度取自 TaxRateFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(t => t.Code)
            .HasMaxLength(TaxRateFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(t => t.Name)
            .HasMaxLength(TaxRateFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(t => t.Rate)
            .HasPrecision(TaxRateFieldConstraints.RatePrecision, TaxRateFieldConstraints.RateDecimalPlaces)
            .IsRequired();
        // 不设数据库默认值：状态由实体属性默认值（Enabled）决定，避免 'Disabled'(0) 命中 CLR 默认值被库默认覆盖
        builder.Property(t => t.Status).HasConversion<short>().IsRequired();
        builder.Property(t => t.Remark)
            .HasMaxLength(TaxRateFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // 编码与名称均全局唯一（大小写敏感；忽略大小写的判定由应用层负责）
        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}