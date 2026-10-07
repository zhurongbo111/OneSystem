using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 工资单表映射配置（表名 Payrolls，唯一索引 (EmployeeId, Year, Month)，员工外键不级联删除）
/// </summary>
internal sealed class PayrollConfiguration : IEntityTypeConfiguration<Payroll>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Payroll> builder)
    {
        builder.ToTable("Payrolls");
        builder.HasKey(p => p.Id);

        // 长度取自 PayrollFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(p => p.EmployeeName)
            .HasMaxLength(EmployeeFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(p => p.Year).IsRequired();
        builder.Property(p => p.Month).IsRequired();
        builder.Property(p => p.BaseSalary).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.Allowance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.Deduction).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.NetPay).HasColumnType("numeric(18,2)").IsRequired();
        // 不设数据库默认值：状态由实体属性默认值（Draft）决定，避免枚举 0 值被库默认覆盖
        builder.Property(p => p.Status).HasConversion<short>().IsRequired();
        builder.Property(p => p.Remark)
            .HasMaxLength(PayrollFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // 一个员工一个月一条（避免重复发薪）；列表按期间 / 员工筛选
        builder.HasIndex(p => new { p.EmployeeId, p.Year, p.Month }).IsUnique();
        builder.HasIndex(p => p.EmployeeId);
        builder.HasIndex(p => new { p.Year, p.Month });

        // 员工不删除（在职 / 离职切换），故外键不级联删除
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
