using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 员工表映射配置（表名 Employees，工号唯一 / 手机 / 邮箱 / 账号「非空时唯一」用部分唯一索引，
/// 部门与岗位外键不级联删除）
/// </summary>
internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);

        // 长度取自 EmployeeFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(e => e.EmployeeNo)
            .HasMaxLength(EmployeeFieldConstraints.NoMaxLength)
            .IsRequired()
            .HasColumnType("varchar(20)");
        builder.Property(e => e.Name)
            .HasMaxLength(EmployeeFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(e => e.Gender).HasConversion<short>();
        builder.Property(e => e.Phone)
            .HasMaxLength(EmployeeFieldConstraints.PhoneMaxLength)
            .HasColumnType("varchar(20)");
        builder.Property(e => e.Email)
            .HasMaxLength(EmployeeFieldConstraints.EmailMaxLength)
            .HasColumnType("varchar(100)");
        builder.Property(e => e.HireDate).IsRequired().HasColumnType("date");
        builder.Property(e => e.ResignDate).HasColumnType("date");
        // 不设数据库默认值：状态由实体属性默认值（Active）决定，避免 'Resigned'(0) 命中 CLR 默认值被库默认覆盖
        builder.Property(e => e.Status).HasConversion<short>().IsRequired();
        builder.Property(e => e.Remark)
            .HasMaxLength(EmployeeFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // 工号唯一（创建后不可改；大小写敏感，忽略大小写的判定由应用层负责）
        builder.HasIndex(e => e.EmployeeNo).IsUnique();

        // 手机 / 邮箱 / 账号可空：仅对非空值建唯一索引（部分索引），NULL 可多条共存
        builder.HasIndex(e => e.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");
        builder.HasIndex(e => e.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        builder.HasIndex(e => e.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");

        // 部门 / 岗位筛选与在职人数统计索引
        builder.HasIndex(e => e.DepartmentId);
        builder.HasIndex(e => e.PositionId);

        // 外键不级联删除：部门 / 岗位删除保护（有员工引用禁止删除）由应用层先校验
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Position>()
            .WithMany()
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        // 账号外键不级联删除：账号仍由 009 维护，员工侧只做绑定
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
