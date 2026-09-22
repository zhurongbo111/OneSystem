using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 凭证分录表映射配置（表名 VoucherEntries；凭证 id / 科目 id 建索引服务详情与余额聚合）
/// （specs/033-erp-general-ledger/design.md §2.3）。
/// </summary>
internal sealed class VoucherEntryConfiguration : IEntityTypeConfiguration<VoucherEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VoucherEntry> builder)
    {
        builder.ToTable("VoucherEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.LineNo).IsRequired();
        builder.Property(e => e.AccountCode)
            .HasMaxLength(AccountFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(e => e.AccountName)
            .HasMaxLength(AccountFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(e => e.Summary)
            .HasMaxLength(VoucherFieldConstraints.EntrySummaryMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(e => e.Debit).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(e => e.Credit).IsRequired().HasColumnType("numeric(18,2)");

        builder.HasIndex(e => e.VoucherId);

        // 科目余额聚合按 AccountId 过滤，建索引
        builder.HasIndex(e => e.AccountId);

        // 主表外键：不级联删除（凭证只作废不删除）
        builder.HasOne<Voucher>()
            .WithMany()
            .HasForeignKey(e => e.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        // 科目外键：不级联删除（删除保护由 IAccountRepository.IsReferencedByVoucherAsync 校验）
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
