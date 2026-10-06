using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Opportunities.CreateOpportunity;
using App.Core.Features.Opportunities.GetOpportunities;
using App.Core.Features.Opportunities.GetOpportunityById;
using App.Core.Features.Opportunities.UpdateOpportunity;
using App.Core.Features.Opportunities.UpdateOpportunityStage;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 商机用例测试（specs/043-erp-crm-presale design.md §6）：
/// 单号与客户名称快照、来源线索 / 客户 / 负责人存在性、全量覆盖语义、
/// 阶段终态限制（终态后不可改阶段，40169）、筛选透传与审计写入。
/// </summary>
public class OpportunityRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

    private static Opportunity NewOpportunity(OpportunityStage stage = OpportunityStage.Initial)
        => new()
        {
            Id = Guid.NewGuid(),
            OpportunityNo = "OP202610060001",
            Name = "甲商机",
            PartnerName = "旧客户",
            Amount = 100m,
            Stage = stage,
            Remark = "备注",
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static Lead NewLead()
        => new()
        {
            Id = Guid.NewGuid(),
            LeadNo = "LD202610060001",
            Name = "甲线索",
            Source = LeadSource.Other,
            Status = LeadStatus.New,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static Employee NewEmployee(AppDbContext context, string name = "李负责")
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = $"E{Guid.NewGuid():N}"[..10],
            Name = name,
            HireDate = new DateOnly(2026, 1, 1),
            Status = EmployeeStatus.Active,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        context.Employees.Add(employee);
        context.SaveChanges();
        return employee;
    }

    private static CreateOpportunityRequestHandler NewCreateHandler(
        AppDbContext context, FakeOpportunityRepository opportunities, FakeLeadRepository leads, RecordingAuditLogger audit)
        => new(
            opportunities,
            leads,
            new PartnerRepository(context),
            new EmployeeRepository(context),
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            audit);

    private static UpdateOpportunityRequestHandler NewUpdateHandler(
        AppDbContext context, FakeOpportunityRepository opportunities, RecordingAuditLogger audit)
        => new(
            opportunities,
            new PartnerRepository(context),
            new EmployeeRepository(context),
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            audit);

    // ============================== 新增 ==============================

    [Fact]
    public async Task 新增商机_应落库客户名称快照与OP单号()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("乙客户", type: PartnerType.Customer);
        var employee = NewEmployee(context);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var opportunities = new FakeOpportunityRepository();
        var audit = new RecordingAuditLogger();

        var result = await NewCreateHandler(context, opportunities, new FakeLeadRepository(), audit).HandleAsync(
            new CreateOpportunityRequest
            {
                Name = "  乙商机  ",
                PartnerId = partner.Id,
                Amount = 8800.50m,
                Stage = OpportunityStage.Requirement,
                ExpectedCloseDate = new DateOnly(2026, 12, 31),
                OwnerId = employee.Id,
                Remark = "  跟进中  ",
            });

        Assert.StartsWith("OP", result.OpportunityNo);
        Assert.Equal("乙商机", result.Name);
        Assert.Equal(partner.Id.ToString(), result.PartnerId);
        Assert.Equal("乙客户", result.PartnerName);
        Assert.Equal(8800.50m, result.Amount);
        Assert.Equal((int)OpportunityStage.Requirement, result.Stage);
        Assert.Equal(new DateOnly(2026, 12, 31), result.ExpectedCloseDate);
        Assert.Equal("跟进中", result.Remark);
        Assert.Equal(new[] { "OP" }, opportunities.GeneratedPrefixes);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Opportunity, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
    }

    [Fact]
    public async Task 新增商机_客户不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewCreateHandler(context, new FakeOpportunityRepository(), new FakeLeadRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateOpportunityRequest
        {
            Name = "乙商机",
            PartnerId = Guid.NewGuid(),
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增商机_来源线索不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewCreateHandler(context, new FakeOpportunityRepository(), new FakeLeadRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateOpportunityRequest
        {
            Name = "乙商机",
            LeadId = Guid.NewGuid(),
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增商机_来源线索存在_应落库线索关联()
    {
        using var context = TestSupport.CreateDbContext();
        var lead = NewLead();
        var leads = new FakeLeadRepository();
        leads.Seed(lead);

        var result = await NewCreateHandler(context, new FakeOpportunityRepository(), leads, new RecordingAuditLogger())
            .HandleAsync(new CreateOpportunityRequest { Name = "乙商机", LeadId = lead.Id });

        Assert.Equal(lead.Id.ToString(), result.LeadId);
    }

    [Fact]
    public async Task 新增商机_负责人不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewCreateHandler(context, new FakeOpportunityRepository(), new FakeLeadRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateOpportunityRequest
        {
            Name = "乙商机",
            OwnerId = Guid.NewGuid(),
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑（全量覆盖）==============================

    [Fact]
    public async Task 编辑商机_客户改则刷新快照且清空可选字段()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("新客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var opportunities = new FakeOpportunityRepository();
        var opportunity = NewOpportunity();
        opportunities.Seed(opportunity);
        var audit = new RecordingAuditLogger();

        var result = await NewUpdateHandler(context, opportunities, audit).HandleAsync(new UpdateOpportunityRequest
        {
            Id = opportunity.Id,
            Name = "甲商机改名",
            PartnerId = partner.Id,
            Amount = 200m,
            Stage = OpportunityStage.Negotiation,
            ExpectedCloseDate = null,
            Remark = "   ",
        });

        Assert.Equal("甲商机改名", result.Name);
        Assert.Equal("新客户", result.PartnerName);
        Assert.Equal(200m, result.Amount);
        Assert.Equal((int)OpportunityStage.Negotiation, result.Stage);
        Assert.Null(result.ExpectedCloseDate);
        Assert.Null(result.Remark);
        Assert.Equal(AuditAction.Update, Assert.Single(audit.Entries).Action);
    }

    [Theory]
    [InlineData(OpportunityStage.Won)]
    [InlineData(OpportunityStage.Lost)]
    public async Task 编辑商机_终态改阶段_应报OpportunityClosed(OpportunityStage stage)
    {
        using var context = TestSupport.CreateDbContext();
        var opportunities = new FakeOpportunityRepository();
        var opportunity = NewOpportunity(stage);
        opportunities.Seed(opportunity);

        var handler = NewUpdateHandler(context, opportunities, new RecordingAuditLogger());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateOpportunityRequest
        {
            Id = opportunity.Id,
            Name = "甲商机改名",
            Amount = 100m,
            Stage = OpportunityStage.Proposal,
        }));

        Assert.Equal(ErrorCode.OpportunityClosed, ex.Code);
    }

    [Fact]
    public async Task 编辑商机_终态但不改阶段_应允许()
    {
        using var context = TestSupport.CreateDbContext();
        var opportunities = new FakeOpportunityRepository();
        var opportunity = NewOpportunity(OpportunityStage.Won);
        opportunities.Seed(opportunity);

        var result = await NewUpdateHandler(context, opportunities, new RecordingAuditLogger())
            .HandleAsync(new UpdateOpportunityRequest
            {
                Id = opportunity.Id,
                Name = "甲商机改名",
                Amount = 150m,
                Stage = OpportunityStage.Won,
            });

        Assert.Equal((int)OpportunityStage.Won, result.Stage);
    }

    [Fact]
    public async Task 编辑商机_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewUpdateHandler(context, new FakeOpportunityRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateOpportunityRequest
        {
            Id = Guid.NewGuid(),
            Name = "甲商机",
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 阶段推进 ==============================

    [Fact]
    public async Task 阶段推进_应落库并写状态变更日志()
    {
        using var context = TestSupport.CreateDbContext();
        var opportunities = new FakeOpportunityRepository();
        var opportunity = NewOpportunity();
        opportunities.Seed(opportunity);
        var audit = new RecordingAuditLogger();

        var handler = new UpdateOpportunityStageRequestHandler(
            opportunities, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new UpdateOpportunityStageRequest
        {
            Id = opportunity.Id,
            Stage = OpportunityStage.Won,
        });

        Assert.Equal((int)OpportunityStage.Won, result.Stage);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Opportunity, entry.Resource);
        Assert.Equal(AuditAction.StatusChange, entry.Action);
    }

    [Theory]
    [InlineData(OpportunityStage.Won)]
    [InlineData(OpportunityStage.Lost)]
    public async Task 阶段推进_终态商机_应报OpportunityClosed(OpportunityStage stage)
    {
        using var context = TestSupport.CreateDbContext();
        var opportunities = new FakeOpportunityRepository();
        var opportunity = NewOpportunity(stage);
        opportunities.Seed(opportunity);

        var handler = new UpdateOpportunityStageRequestHandler(
            opportunities, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateOpportunityStageRequest
        {
            Id = opportunity.Id,
            Stage = OpportunityStage.Negotiation,
        }));

        Assert.Equal(ErrorCode.OpportunityClosed, ex.Code);
    }

    [Fact]
    public async Task 阶段推进_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = new UpdateOpportunityStageRequestHandler(
            new FakeOpportunityRepository(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateOpportunityStageRequest
        {
            Id = Guid.NewGuid(),
            Stage = OpportunityStage.Requirement,
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 列表与详情 ==============================

    [Fact]
    public async Task 商机列表_筛选条件应透传仓储且带出负责人姓名()
    {
        var opportunities = new FakeOpportunityRepository();
        var ownerId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var opportunity = NewOpportunity(OpportunityStage.Negotiation);
        IReadOnlyList<OpportunityListItem> items =
            new[] { new OpportunityListItem { Opportunity = opportunity, OwnerName = "李负责" } };
        opportunities.PagedResult = (items, 1);

        var handler = new GetOpportunitiesRequestHandler(opportunities);
        var result = await handler.HandleAsync(new GetOpportunitiesRequest
        {
            Page = 3,
            PageSize = 5,
            Keyword = "甲",
            Stage = OpportunityStage.Negotiation,
            PartnerId = partnerId,
            OwnerId = ownerId,
        });

        Assert.Equal(1, result.Total);
        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal("李负责", result.Items[0].OwnerName);

        var args = opportunities.LastPagedArgs!.Value;
        Assert.Equal("甲", args.Keyword);
        Assert.Equal(OpportunityStage.Negotiation, args.Stage);
        Assert.Equal(partnerId, args.PartnerId);
        Assert.Equal(ownerId, args.OwnerId);
        Assert.Equal(3, args.Page);
        Assert.Equal(5, args.PageSize);
    }

    [Fact]
    public async Task 商机详情_不存在_应报40400()
    {
        var handler = new GetOpportunityByIdRequestHandler(new FakeOpportunityRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetOpportunityByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
