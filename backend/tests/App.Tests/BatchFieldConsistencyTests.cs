using App.Core.Entities;
using App.Core.Features.Batches.CreateBatch;
using App.Core.Features.Batches.UpdateBatch;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 批次字段约束一致性测试（040 §2.6）：保证批次号长度 / 格式 / 备注长度与数据库列长同源，
/// 且各 Validator 边界与 BatchFieldConstraints 常量一致。
/// 守护点：
/// 1. BatchNo 列长 = BatchFieldConstraints.BatchNoMaxLength；
/// 2. Remark 列长 = OrderFieldConstraints.RemarkMaxLength（对齐单据域）；
/// 3. 批次号边界（50 通过 / 51 拒绝）与常量一致；非法字符 / 空串拒绝；
/// 4. 备注边界（200 通过 / 201 拒绝）；
/// 5. 编辑批次：到期日不能早于生产日期（仅两者同时提供时）。
/// </summary>
public class BatchFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    [Fact]
    public void 批次号列长_应等于常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        Assert.Equal(BatchFieldConstraints.BatchNoMaxLength, GetMaxLength<Batch>(dbContext, nameof(Batch.BatchNo)));
    }

    [Fact]
    public void 备注列长_应等于单据域备注常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<Batch>(dbContext, nameof(Batch.Remark)));
    }

    [Fact]
    public void 新增批次_批次号长度边界_应通过_越界应拒绝()
    {
        var validator = new CreateBatchRequestValidator();
        var product = Guid.NewGuid();
        var ok = new string('a', BatchFieldConstraints.BatchNoMaxLength);
        var tooLong = new string('a', BatchFieldConstraints.BatchNoMaxLength + 1);

        Assert.True(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = ok }).IsValid);
        Assert.False(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = tooLong }).IsValid);
    }

    [Fact]
    public void 新增批次_批次号格式_非法字符与空串应拒绝()
    {
        var validator = new CreateBatchRequestValidator();
        var product = Guid.NewGuid();

        // 合法字符（字母 / 数字 / 下划线 / 连字符）
        Assert.True(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = "B-2026_01" }).IsValid);
        // 含中文 / 空格 / 特殊字符
        Assert.False(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = "批次 01" }).IsValid);
        Assert.False(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = "B/01" }).IsValid);
        // 空串
        Assert.False(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = "" }).IsValid);
    }

    [Fact]
    public void 新增批次_备注长度边界_应通过_越界应拒绝()
    {
        var validator = new CreateBatchRequestValidator();
        var product = Guid.NewGuid();
        var ok = new string('x', OrderFieldConstraints.RemarkMaxLength);
        var tooLong = new string('x', OrderFieldConstraints.RemarkMaxLength + 1);

        Assert.True(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = "B1", Remark = ok }).IsValid);
        Assert.False(validator.Validate(new CreateBatchRequest { ProductId = product, BatchNo = "B1", Remark = tooLong }).IsValid);
    }

    [Fact]
    public void 编辑批次_到期日早于生产日期_应拒绝_同时为空应通过()
    {
        var validator = new UpdateBatchRequestValidator();

        // 到期日 < 生产日期
        Assert.False(validator.Validate(new UpdateBatchRequest
        {
            Id = Guid.NewGuid(),
            ProductionDate = new DateTime(2026, 1, 10),
            ExpiryDate = new DateTime(2026, 1, 1),
        }).IsValid);

        // 到期日 = 生产日期（同日允许）
        Assert.True(validator.Validate(new UpdateBatchRequest
        {
            Id = Guid.NewGuid(),
            ProductionDate = new DateTime(2026, 1, 1),
            ExpiryDate = new DateTime(2026, 1, 1),
        }).IsValid);

        // 仅到期日为空（无生产日期）→ 不触发
        Assert.True(validator.Validate(new UpdateBatchRequest { Id = Guid.NewGuid(), ExpiryDate = new DateTime(2026, 1, 1) }).IsValid);
    }
}
