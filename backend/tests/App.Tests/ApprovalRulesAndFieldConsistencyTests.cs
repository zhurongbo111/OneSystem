using App.Core.Entities;
using App.Core.Features.Approvals.ApproveOrder;
using App.Core.Features.Approvals.GetApprovalRules;
using App.Core.Features.Approvals.GetApprovals;
using App.Core.Features.Approvals.RejectApproval;
using App.Core.Features.Approvals.UpdateApprovalRules;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 审批规则用例与字段约束一致性测试（specs/042-erp-approval tasks 4.4 / 4.6）：
/// 缺失规则返回默认「未启用」、upsert 新建与更新、阈值与类型去重边界；
/// EF 列长 / 唯一索引与常量一致；请求校验器的必填与长度边界。
/// </summary>
public class ApprovalRulesAndFieldConsistencyTests
{
    // ============================== 规则查询 ==============================

    [Fact]
    public async Task 规则查询_未配置_应返回四类默认未启用()
    {
        var handler = new GetApprovalRulesRequestHandler(new FakeApprovalRuleRepository());

        var rules = await handler.HandleAsync(new GetApprovalRulesRequest());

        Assert.Equal(4, rules.Count);
        Assert.All(rules, rule =>
        {
            Assert.Equal(0m, rule.ThresholdAmount);
            Assert.False(rule.Enabled);
        });
        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            rules.Select(r => r.OrderType).ToArray());
    }

    [Fact]
    public async Task 规则查询_已配置_应回填阈值与启用()
    {
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.PurchaseInbound, 1000m, enabled: true);
        rules.Seed(SettlementOrderType.SalesReturn, 5000m, enabled: false);

        var result = await new GetApprovalRulesRequestHandler(rules).HandleAsync(new GetApprovalRulesRequest());

        Assert.Equal(1000m, result.Single(r => r.OrderType == 0).ThresholdAmount);
        Assert.True(result.Single(r => r.OrderType == 0).Enabled);
        Assert.Equal(5000m, result.Single(r => r.OrderType == 3).ThresholdAmount);
        Assert.False(result.Single(r => r.OrderType == 3).Enabled);
    }

    // ============================== 规则保存 ==============================

    [Fact]
    public async Task 规则保存_应逐类型upsert并可回读()
    {
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.PurchaseInbound, 1000m, enabled: true);

        var handler = new UpdateApprovalRulesRequestHandler(
            rules, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new UpdateApprovalRulesRequest
        {
            Rules =
            [
                new UpdateApprovalRuleItem { OrderType = SettlementOrderType.PurchaseInbound, ThresholdAmount = 2000m, Enabled = false },
                new UpdateApprovalRuleItem { OrderType = SettlementOrderType.SalesOutbound, ThresholdAmount = 300m, Enabled = true },
            ],
        });

        Assert.Equal(2, rules.Upserts.Count);
        Assert.Equal(2000m, result.Single(r => r.OrderType == 0).ThresholdAmount);  // 已存在 → 更新
        Assert.False(result.Single(r => r.OrderType == 0).Enabled);
        Assert.True(result.Single(r => r.OrderType == 1).Enabled);                  // 不存在 → 新建
        Assert.Equal(4, result.Count);                                             // 未提交的类型仍返回默认行
    }

    // ============================== 校验器边界 ==============================

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(10000000, false)]
    public void 规则保存_阈值边界_应通过_越界应拒绝(double threshold, bool expected)
    {
        var validator = new UpdateApprovalRulesRequestValidator();
        var request = new UpdateApprovalRulesRequest
        {
            Rules =
            [
                new UpdateApprovalRuleItem
                {
                    OrderType = SettlementOrderType.PurchaseInbound,
                    ThresholdAmount = (decimal)threshold,
                    Enabled = true,
                },
            ],
        };

        Assert.Equal(expected, validator.Validate(request).IsValid);
    }

    [Fact]
    public void 规则保存_单据类型重复_应拒绝()
    {
        var validator = new UpdateApprovalRulesRequestValidator();
        var request = new UpdateApprovalRulesRequest
        {
            Rules =
            [
                new UpdateApprovalRuleItem { OrderType = SettlementOrderType.PurchaseInbound, ThresholdAmount = 1m, Enabled = true },
                new UpdateApprovalRuleItem { OrderType = SettlementOrderType.PurchaseInbound, ThresholdAmount = 2m, Enabled = false },
            ],
        };

        Assert.False(validator.Validate(request).IsValid);
    }

    [Fact]
    public void 规则保存_超过四项_应拒绝()
    {
        var validator = new UpdateApprovalRulesRequestValidator();
        var request = new UpdateApprovalRulesRequest
        {
            Rules = Enumerable.Range(0, 5)
                .Select(index => new UpdateApprovalRuleItem
                {
                    OrderType = SettlementOrderType.PurchaseInbound,
                    ThresholdAmount = 1m,
                    Enabled = index % 2 == 0,
                })
                .ToList(),
        };

        Assert.False(validator.Validate(request).IsValid);
    }

    [Fact]
    public void 驳回_意见必填且不超过备注列长()
    {
        var validator = new RejectApprovalRequestValidator();

        Assert.False(validator.Validate(new RejectApprovalRequest { Remark = null }).IsValid);
        Assert.False(validator.Validate(new RejectApprovalRequest { Remark = "   " }).IsValid);
        Assert.True(validator.Validate(new RejectApprovalRequest { Remark = "不批" }).IsValid);
        Assert.True(validator.Validate(new RejectApprovalRequest
        {
            Remark = new string('a', OrderFieldConstraints.RemarkMaxLength),
        }).IsValid);
        Assert.False(validator.Validate(new RejectApprovalRequest
        {
            Remark = new string('a', OrderFieldConstraints.RemarkMaxLength + 1),
        }).IsValid);
    }

    [Fact]
    public void 通过_意见可空且不超过备注列长()
    {
        var validator = new ApproveOrderRequestValidator();

        Assert.True(validator.Validate(new ApproveOrderRequest()).IsValid);
        Assert.True(validator.Validate(new ApproveOrderRequest
        {
            Remark = new string('a', OrderFieldConstraints.RemarkMaxLength),
        }).IsValid);
        Assert.False(validator.Validate(new ApproveOrderRequest
        {
            Remark = new string('a', OrderFieldConstraints.RemarkMaxLength + 1),
        }).IsValid);
    }

    [Fact]
    public void 审批列表_分页与取值边界_应通过_越界应拒绝()
    {
        var validator = new GetApprovalsRequestValidator();

        Assert.True(validator.Validate(new GetApprovalsRequest { Page = 1, PageSize = 100 }).IsValid);
        Assert.False(validator.Validate(new GetApprovalsRequest { Page = 0 }).IsValid);
        Assert.False(validator.Validate(new GetApprovalsRequest { PageSize = 101 }).IsValid);

        Assert.True(validator.Validate(new GetApprovalsRequest { Status = ApprovalStatus.Pending }).IsValid);
        Assert.False(validator.Validate(new GetApprovalsRequest { Status = ApprovalStatus.None }).IsValid);
        Assert.True(validator.Validate(new GetApprovalsRequest { OrderType = SettlementOrderType.SalesReturn }).IsValid);

        // 时间范围：结束早于开始 → 拒绝
        Assert.False(validator.Validate(new GetApprovalsRequest
        {
            Start = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        }).IsValid);
    }

    // ============================== 字段约束一致性 ==============================

    [Fact]
    public void 审批记录_列长与唯一索引_应与常量一致()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(Approval))!;

        Assert.Equal(
            OrderFieldConstraints.OrderNoMaxLength,
            entityType.FindProperty(nameof(Approval.OrderNo))!.GetMaxLength());
        Assert.Equal(
            OrderFieldConstraints.RemarkMaxLength,
            entityType.FindProperty(nameof(Approval.DecisionRemark))!.GetMaxLength());
        Assert.Equal(
            PartnerFieldConstraints.NameMaxLength,
            entityType.FindProperty(nameof(Approval.PartnerName))!.GetMaxLength());

        // 一张单据一条审批记录
        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index => index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(Approval.OrderType), nameof(Approval.OrderId) }));
        Assert.NotNull(uniqueIndex);
    }

    [Fact]
    public void 审批规则_单据类型唯一索引_应存在()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(ApprovalRule))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index => index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(ApprovalRule.OrderType) }));
        Assert.NotNull(uniqueIndex);
        // 阈值为金额列：numeric(18,2)（与各单据金额列同口径；InMemory 无关系型映射，直接读列类型注解）
        var columnType = entityType.FindProperty(nameof(ApprovalRule.ThresholdAmount))!
            .FindAnnotation("Relational:ColumnType")?.Value as string;
        Assert.Equal("numeric(18,2)", columnType);
    }

    [Fact]
    public void 四类单据_均应带审批状态列()
    {
        using var dbContext = TestSupport.CreateDbContext();

        foreach (var type in new[]
        {
            typeof(PurchaseReceipt), typeof(SalesShipment), typeof(PurchaseReturn), typeof(SalesReturn),
        })
        {
            var property = dbContext.Model.FindEntityType(type)!.FindProperty(nameof(PurchaseReceipt.ApprovalStatus));
            Assert.NotNull(property);
            Assert.False(property!.IsNullable);
        }
    }
}
