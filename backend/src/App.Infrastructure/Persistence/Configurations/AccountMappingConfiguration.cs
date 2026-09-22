using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 科目映射表映射配置（表名 AccountMappings，映射键唯一索引）
/// （specs/033-erp-general-ledger/design.md §2.4）。
/// </summary>
internal sealed class AccountMappingConfiguration : IEntityTypeConfiguration<AccountMapping>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountMapping> builder)
    {
        builder.ToTable("AccountMappings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Key)
            .HasMaxLength(AccountMappingFieldConstraints.KeyMaxLength)
            .IsRequired()
            .HasColumnType("varchar(30)");
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        // 映射键唯一：自动凭证按键取科目
        builder.HasIndex(m => m.Key).IsUnique();

        // 目标科目外键：不级联删除（科目删除保护由 IAccountRepository 校验）
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
