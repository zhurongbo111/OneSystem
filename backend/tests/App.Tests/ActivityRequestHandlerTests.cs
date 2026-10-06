using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Activities.CreateLeadActivity;
using App.Core.Features.Activities.CreateOpportunityActivity;
using App.Core.Features.Activities.GetLeadActivities;
using App.Core.Features.Activities.GetOpportunityActivities;

namespace App.Tests;

/// <summary>
/// 跟进活动用例测试（specs/043-erp-crm-presale design.md §6）：
/// 按域归属新增与查询（线索 / 商机互不串数据）、归属业务存在性（40400）、
/// 记录人带出与审计写入；活动无改删端点（只增）。
/// </summary>
public class ActivityRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

    private static Lead NewLead(string no = "LD202610060001")
        => new()
        {
            Id = Guid.NewGuid(),
            LeadNo = no,
            Name = "甲线索",
            Source = LeadSource.Other,
            Status = LeadStatus.New,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static Opportunity NewOpportunity(string no = "OP202610060001")
        => new()
        {
            Id = Guid.NewGuid(),
            OpportunityNo = no,
            Name = "甲商机",
            Amount = 0m,
            Stage = OpportunityStage.Initial,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static Activity NewActivity(ActivityBizType bizType, Guid bizId, DateTimeOffset time, string content)
        => new()
        {
            Id = Guid.NewGuid(),
            BizType = bizType,
            BizId = bizId,
            Type = ActivityType.Call,
            Content = content,
            ActivityTime = time,
            CreatedAt = Now,
        };

    // ============================== 新增（线索）==============================

    [Fact]
    public async Task 新增线索跟进活动_应归属线索并带出记录人()
    {
        var lead = NewLead();
        var leads = new FakeLeadRepository();
        leads.Seed(lead);
        var activities = new FakeActivityRepository();
        var audit = new RecordingAuditLogger();
        var user = new StubCurrentUser(Guid.NewGuid(), displayName: "销售甲");

        var handler = new CreateLeadActivityRequestHandler(
            leads, activities, new RecordingUnitOfWork(), user, audit);

        var result = await handler.HandleAsync(new CreateLeadActivityRequest
        {
            LeadId = lead.Id,
            Type = ActivityType.Visit,
            Content = "  上门拜访，确认预算  ",
            ActivityTime = Now,
        });

        Assert.Equal((int)ActivityBizType.Lead, result.BizType);
        Assert.Equal(lead.Id.ToString(), result.BizId);
        Assert.Equal((int)ActivityType.Visit, result.Type);
        Assert.Equal("上门拜访，确认预算", result.Content);
        Assert.Equal("销售甲", result.RecorderName);

        var stored = Assert.Single(activities.Activities);
        Assert.Equal(ActivityBizType.Lead, stored.BizType);
        Assert.Equal(lead.Id, stored.BizId);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Activity, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Equal(lead.LeadNo, entry.ResourceNo);
    }

    [Fact]
    public async Task 新增线索跟进活动_线索不存在_应报40400()
    {
        var handler = new CreateLeadActivityRequestHandler(
            new FakeLeadRepository(), new FakeActivityRepository(), new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateLeadActivityRequest
        {
            LeadId = Guid.NewGuid(),
            Content = "电话跟进",
            ActivityTime = Now,
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 新增（商机）==============================

    [Fact]
    public async Task 新增商机跟进活动_应归属商机并带出记录人()
    {
        var opportunity = NewOpportunity();
        var opportunities = new FakeOpportunityRepository();
        opportunities.Seed(opportunity);
        var activities = new FakeActivityRepository();
        var audit = new RecordingAuditLogger();

        var handler = new CreateOpportunityActivityRequestHandler(
            opportunities, activities, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new CreateOpportunityActivityRequest
        {
            OpportunityId = opportunity.Id,
            Type = ActivityType.Email,
            Content = "发送方案报价",
            ActivityTime = Now,
        });

        Assert.Equal((int)ActivityBizType.Opportunity, result.BizType);
        Assert.Equal(opportunity.Id.ToString(), result.BizId);
        Assert.Equal(opportunity.OpportunityNo, Assert.Single(audit.Entries).ResourceNo);
    }

    [Fact]
    public async Task 新增商机跟进活动_商机不存在_应报40400()
    {
        var handler = new CreateOpportunityActivityRequestHandler(
            new FakeOpportunityRepository(), new FakeActivityRepository(), new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateOpportunityActivityRequest
        {
            OpportunityId = Guid.NewGuid(),
            Content = "电话跟进",
            ActivityTime = Now,
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 查询（按域归属）==============================

    [Fact]
    public async Task 线索活动查询_只返回该线索的活动且按跟进时间倒序()
    {
        var lead = NewLead();
        var otherLead = NewLead("LD202610060002");
        var opportunity = NewOpportunity();
        var leads = new FakeLeadRepository();
        leads.Seed(lead);

        var activities = new FakeActivityRepository();
        activities.Seed(NewActivity(ActivityBizType.Lead, lead.Id, Now.AddDays(-1), "第一次电话"));
        activities.Seed(NewActivity(ActivityBizType.Lead, lead.Id, Now, "第二次电话"));
        activities.Seed(NewActivity(ActivityBizType.Lead, otherLead.Id, Now, "别的线索"));
        activities.Seed(NewActivity(ActivityBizType.Opportunity, opportunity.Id, Now, "商机的活动"));

        var handler = new GetLeadActivitiesRequestHandler(leads, activities);
        var result = await handler.HandleAsync(new GetLeadActivitiesRequest { LeadId = lead.Id });

        Assert.Equal(2, result.Count);
        Assert.Equal("第二次电话", result[0].Content);
        Assert.Equal("第一次电话", result[1].Content);
        Assert.All(result, x => Assert.Equal(lead.Id.ToString(), x.BizId));
    }

    [Fact]
    public async Task 商机活动查询_只返回该商机的活动()
    {
        var lead = NewLead();
        var opportunity = NewOpportunity();
        var opportunities = new FakeOpportunityRepository();
        opportunities.Seed(opportunity);

        var activities = new FakeActivityRepository();
        activities.Seed(NewActivity(ActivityBizType.Opportunity, opportunity.Id, Now, "商务谈判"));
        activities.Seed(NewActivity(ActivityBizType.Lead, lead.Id, Now, "线索的活动"));

        var handler = new GetOpportunityActivitiesRequestHandler(opportunities, activities);
        var result = await handler.HandleAsync(new GetOpportunityActivitiesRequest { OpportunityId = opportunity.Id });

        var single = Assert.Single(result);
        Assert.Equal("商务谈判", single.Content);
        Assert.Equal((int)ActivityBizType.Opportunity, single.BizType);
    }

    [Fact]
    public async Task 线索活动查询_线索不存在_应报40400()
    {
        var handler = new GetLeadActivitiesRequestHandler(new FakeLeadRepository(), new FakeActivityRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetLeadActivitiesRequest { LeadId = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 商机活动查询_商机不存在_应报40400()
    {
        var handler = new GetOpportunityActivitiesRequestHandler(new FakeOpportunityRepository(), new FakeActivityRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetOpportunityActivitiesRequest { OpportunityId = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
