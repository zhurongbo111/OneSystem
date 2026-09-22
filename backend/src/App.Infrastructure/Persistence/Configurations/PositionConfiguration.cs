using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 岗位表映射配置（表名 Positions，编码与名称唯一索引 / 状态默认启用）
/// </summary>
internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.HasKey(p => p.Id);

        // 长度取自 PositionFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(p => p.Code)
            .HasMaxLength(PositionFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(p => p.Name)
            .HasMaxLength(PositionFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        // 不设数据库默认值：状态由实体属性默认值（Enabled）决定，避免 'Disabled'(0) 命中 CLR 默认值被库默认覆盖
        builder.Property(p => p.Status).HasConversion<short>().IsRequired();
        builder.Property(p => p.Remark)
            .HasMaxLength(PositionFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.Name).IsUnique();
    }
}
