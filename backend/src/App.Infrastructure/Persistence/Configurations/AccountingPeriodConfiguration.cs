using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 会计期间表映射配置（表名 AccountingPeriods，「年-月」唯一索引）
/// （specs/033-erp-general-ledger/design.md §2.1）。
/// </summary>
internal sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("AccountingPeriods");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Year).IsRequired();
        builder.Property(p => p.Month).IsRequired();
        builder.Property(p => p.Status).HasConversion<short>().IsRequired();

        // 「年-月」唯一：凭证归属期间按 VoucherDate 的年月定位
        builder.HasIndex(p => new { p.Year, p.Month }).IsUnique();
    }
}
