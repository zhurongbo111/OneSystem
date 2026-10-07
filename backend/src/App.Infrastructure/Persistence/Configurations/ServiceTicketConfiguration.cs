using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 服务工单表映射配置（表名 ServiceTickets，工单号唯一索引；外键不级联删除）。
/// 列长统一取自 <see cref="ServiceTicketFieldConstraints"/>（单一来源，specs/045-erp-crm-service design.md §2.1 / §2.2）。
/// </summary>
internal sealed class ServiceTicketConfiguration : IEntityTypeConfiguration<ServiceTicket>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ServiceTicket> builder)
    {
        builder.ToTable("ServiceTickets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNo)
            .HasMaxLength(ServiceTicketFieldConstraints.NoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(t => t.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(t => t.Contact)
            .HasMaxLength(ServiceTicketFieldConstraints.ContactMaxLength)
            .HasColumnType("varchar(30)");
        builder.Property(t => t.Phone)
            .HasMaxLength(ServiceTicketFieldConstraints.PhoneMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(t => t.Title)
            .HasMaxLength(ServiceTicketFieldConstraints.TitleMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(t => t.Description)
            .HasMaxLength(ServiceTicketFieldConstraints.DescriptionMaxLength)
            .HasColumnType("varchar(500)");
        builder.Property(t => t.Priority).HasConversion<short>().IsRequired();
        builder.Property(t => t.Status).HasConversion<short>().IsRequired().HasDefaultValue(TicketStatus.Pending);
        builder.Property(t => t.ResolvedAt);
        builder.Property(t => t.Remark)
            .HasMaxLength(ServiceTicketFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // 工单号唯一（并发兜底，生成机制见 specs/015-erp-purchase design.md §3.6）
        builder.HasIndex(t => t.TicketNo).IsUnique();

        // 客户筛选 / 负责人筛选
        builder.HasIndex(t => t.PartnerId);
        builder.HasIndex(t => t.OwnerId);

        // 外键：均不级联删除（客户 / 员工不可因删除而带走服务记录）
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(t => t.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
