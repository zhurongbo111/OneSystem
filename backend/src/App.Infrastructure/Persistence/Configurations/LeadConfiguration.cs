using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 线索表映射配置（表名 Leads，单号唯一索引；外键不级联删除）。
/// 列长统一取自 <see cref="LeadFieldConstraints"/>（单一来源，specs/043-erp-crm-presale design.md §2.1 / §2.4）。
/// </summary>
internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LeadNo)
            .HasMaxLength(LeadFieldConstraints.NoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(l => l.Name)
            .HasMaxLength(LeadFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(l => l.Contact)
            .HasMaxLength(LeadFieldConstraints.ContactMaxLength)
            .HasColumnType("varchar(30)");
        builder.Property(l => l.Phone)
            .HasMaxLength(LeadFieldConstraints.PhoneMaxLength)
            .HasColumnType("varchar(20)");
        // 来源默认「其他」由实体初始化器给出（不落库列默认值：Other = 4 与枚举 CLR 默认值 0 不同，
        // 落库默认值会让 Source = Website(0) 的插入被数据库默认覆盖，见 EF 警告 20601）
        builder.Property(l => l.Source).HasConversion<short>().IsRequired();
        builder.Property(l => l.Status).HasConversion<short>().IsRequired().HasDefaultValue(LeadStatus.New);
        builder.Property(l => l.OpportunityNo)
            .HasMaxLength(LeadFieldConstraints.NoMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(l => l.Remark)
            .HasMaxLength(LeadFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，生成机制见 specs/015-erp-purchase design.md §3.6）
        builder.HasIndex(l => l.LeadNo).IsUnique();

        // 负责人筛选 / 列表联查
        builder.HasIndex(l => l.OwnerId);

        // 负责人外键：不级联删除（员工只离职不删除）
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(l => l.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
