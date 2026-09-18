using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence.Configurations;

/// <summary>
/// 盘点 / 期初建账明细表映射配置（表名 StockTakeItems，StockTakeId 索引；外键不级联删除）。
/// 编码 / 名称 / 单位为提交时快照，长度取自 ProductFieldConstraints（商品档案列同源）；
/// 明细不可改、不软删除（一步式）。
/// </summary>
internal sealed class StockTakeItemConfiguration : IEntityTypeConfiguration<StockTakeItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StockTakeItem> builder)
    {
        builder.ToTable("StockTakeItems");
        builder.HasKey(i => i.Id);

        // 列长取自 ProductFieldConstraints（商品档案列，快照同源）
        builder.Property(i => i.ProductCode)
            .HasMaxLength(ProductFieldConstraints.CodeMaxLength)
            .IsRequired()
            .HasColumnType("varchar(32)");
        builder.Property(i => i.ProductName)
            .HasMaxLength(ProductFieldConstraints.NameMaxLength)
            .IsRequired()
            .HasColumnType("varchar(50)");
        builder.Property(i => i.Unit)
            .HasMaxLength(ProductFieldConstraints.UnitMaxLength)
            .IsRequired()
            .HasColumnType("varchar(10)");
        builder.Property(i => i.BookQuantity).IsRequired();
        builder.Property(i => i.ActualQuantity).IsRequired();
        builder.Property(i => i.Difference).IsRequired();

        // 期初成本单价（erp-cost）：numeric(18,4)，默认 0；仅期初建账模式由前端录入
        builder.Property(i => i.UnitCost)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.HasIndex(i => i.StockTakeId);

        // 商品停用不影响历史盘点凭证 → 外键禁止级联删除
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
