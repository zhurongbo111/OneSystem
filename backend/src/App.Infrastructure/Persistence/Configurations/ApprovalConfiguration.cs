using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 审批记录表映射配置（表名 Approvals，specs/042-erp-approval/design.md §2.2）：
/// 一张单据一条记录（唯一索引 <c>(OrderType, OrderId)</c>）；单号 / 往来名称 / 金额为快照，
/// 列长取自 <see cref="OrderFieldConstraints"/> / <see cref="PartnerFieldConstraints"/>（单一来源）。
/// </summary>
internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("Approvals");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OrderType).HasConversion<short>().IsRequired();
        builder.Property(a => a.OrderNo)
            .HasMaxLength(OrderFieldConstraints.OrderNoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(a => a.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(a => a.Amount).IsRequired().HasColumnType("numeric(18,2)");
        // 不设数据库默认值：记录状态由代码显式写入（Pending / Approved / Rejected / Withdrawn，永不写 None）
        builder.Property(a => a.Status).HasConversion<short>().IsRequired();
        builder.Property(a => a.SubmittedBy).IsRequired();
        builder.Property(a => a.SubmittedAt).IsRequired();
        builder.Property(a => a.DecidedAt);
        builder.Property(a => a.DecisionRemark)
            .HasMaxLength(OrderFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");

        // 一张单据一条审批记录（驳回 / 撤回后不再重新提交）
        builder.HasIndex(a => new { a.OrderType, a.OrderId }).IsUnique();

        // 「待我审批」列表：状态 + 提交时间倒序
        builder.HasIndex(a => new { a.Status, a.SubmittedAt }).IsDescending(false, true);

        // 提交人筛选（撤回归属校验）
        builder.HasIndex(a => a.SubmittedBy);
    }
}
