using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 资金账户表映射配置（表名 BankAccounts，编码唯一索引 / 初始余额 numeric(18,2)）
/// （specs/034-erp-cash/design.md §2.1）。
/// </summary>
internal sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");
        builder.HasKey(b => b.Id);

        // 长度 / 精度取自 BankAccountFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(b => b.Code)
            .HasMaxLength(BankAccountFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(b => b.Name)
            .HasMaxLength(BankAccountFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(b => b.Type).HasConversion<short>().IsRequired();
        builder.Property(b => b.BankName)
            .HasMaxLength(BankAccountFieldConstraints.BankNameMaxLength)
            .HasColumnType("varchar(100)");
        builder.Property(b => b.AccountNo)
            .HasMaxLength(BankAccountFieldConstraints.AccountNoMaxLength)
            .HasColumnType("varchar(30)");
        builder.Property(b => b.InitialBalance)
            .HasPrecision(BankAccountFieldConstraints.AmountPrecision, BankAccountFieldConstraints.AmountDecimalPlaces)
            .IsRequired();
        builder.Property(b => b.Status).HasConversion<short>().IsRequired();
        builder.Property(b => b.Remark)
            .HasMaxLength(BankAccountFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        // 编码全局唯一（大小写敏感；忽略大小写的判定由应用层负责）
        builder.HasIndex(b => b.Code).IsUnique();
    }
}
