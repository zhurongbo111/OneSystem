using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 考勤登记表映射配置（表名 Attendances，员工外键不级联删除；姓名快照列长与员工姓名一致）
/// </summary>
internal sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendances");
        builder.HasKey(a => a.Id);

        // 长度取自 AttendanceFieldConstraints（单一来源），与各 RequestValidator 保持一致
        builder.Property(a => a.EmployeeName)
            .HasMaxLength(EmployeeFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(a => a.Type).HasConversion<short>().IsRequired();
        builder.Property(a => a.StartDate).IsRequired().HasColumnType("date");
        builder.Property(a => a.EndDate).IsRequired().HasColumnType("date");
        builder.Property(a => a.Remark)
            .HasMaxLength(AttendanceFieldConstraints.RemarkMaxLength)
            .HasColumnType("varchar(200)");
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        // 列表按员工筛选、区间重叠校验按 (员工 + 类型 + 日期区间) 查询
        builder.HasIndex(a => a.EmployeeId);

        // 员工不删除（在职 / 离职切换），故外键不级联删除
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
