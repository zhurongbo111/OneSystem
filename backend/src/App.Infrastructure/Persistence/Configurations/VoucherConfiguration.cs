using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 记账凭证主表映射配置（表名 Vouchers，凭证号唯一索引；来源单据 id 建索引服务「随单据作废」反查）
/// （specs/033-erp-general-ledger/design.md §2.2）。
/// </summary>
internal sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.VoucherNo)
            .HasMaxLength(VoucherFieldConstraints.NoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(30)");
        builder.Property(v => v.VoucherDate).IsRequired();
        builder.Property(v => v.Summary)
            .HasMaxLength(VoucherFieldConstraints.SummaryMaxLength)
            .IsRequired()
            .HasColumnType("varchar(200)");
        builder.Property(v => v.SourceType).HasConversion<short>().IsRequired();
        builder.Property(v => v.SourceNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(v => v.TotalDebit).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(v => v.TotalCredit).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(v => v.Status).HasConversion<short>().IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt).IsRequired();

        // 凭证号全局唯一（并发兜底：冲突由仓储包装为唯一约束异常，Handler 重试生成）
        builder.HasIndex(v => v.VoucherNo).IsUnique();

        // 来源单据 id 索引：单据作废时按 (SourceType, SourceId) 反查其自动凭证
        builder.HasIndex(v => v.SourceId);

        // 归属期间外键：不级联删除（期间只结账，不删除）
        builder.HasOne<AccountingPeriod>()
            .WithMany()
            .HasForeignKey(v => v.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
