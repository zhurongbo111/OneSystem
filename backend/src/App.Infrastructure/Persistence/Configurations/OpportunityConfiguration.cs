using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 商机表映射配置（表名 Opportunities，单号唯一索引；外键不级联删除）。
/// 列长统一取自 <see cref="OpportunityFieldConstraints"/> 与 <see cref="PartnerFieldConstraints"/>（单一来源，specs/043-erp-crm-presale design.md §2.2 / §2.4）。
/// </summary>
internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("Opportunities");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OpportunityNo)
            .HasMaxLength(OpportunityFieldConstraints.NoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(o => o.Name)
            .HasMaxLength(OpportunityFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(o => o.PartnerName)
            .HasMaxLength(PartnerFieldConstraints.NameMaxLength)
            .HasColumnType("varchar(50)");
        builder.Property(o => o.Amount).IsRequired().HasColumnType("numeric(18,2)");
        builder.Property(o => o.Stage).HasConversion<short>().IsRequired().HasDefaultValue(OpportunityStage.Initial);
        builder.Property(o => o.ExpectedCloseDate).HasColumnType("date");
        builder.Property(o => o.Remark)
            .HasMaxLength(OpportunityFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        // 单号唯一（并发兜底，生成机制见 specs/015-erp-purchase design.md §3.6）
        builder.HasIndex(o => o.OpportunityNo).IsUnique();

        // 客户筛选 / 列表展示
        builder.HasIndex(o => o.PartnerId);

        // 负责人筛选 / 列表联查
        builder.HasIndex(o => o.OwnerId);

        // 来源线索外键：不级联删除（线索永不物理删除）
        builder.HasOne<Lead>()
            .WithMany()
            .HasForeignKey(o => o.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        // 客户外键：可空、不级联删除（客户只停用不删除）
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(o => o.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 负责人外键：不级联删除（员工只离职不删除）
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(o => o.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
