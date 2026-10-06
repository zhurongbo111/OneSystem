using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Leads.ConvertLead;
using App.Core.Features.Leads.CreateLead;
using App.Core.Features.Leads.GetLeadById;
using App.Core.Features.Leads.GetLeads;
using App.Core.Features.Leads.UpdateLead;
using App.Core.Features.Leads.UpdateLeadStatus;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 线索用例测试（specs/043-erp-crm-presale design.md §6）：
/// 单号与落库字段、全量覆盖语义、状态流转限制（终态 40168）、转商机原子（商机 + 线索回写同一事务、
/// 不触碰库存 / 资金）、筛选透传与审计写入。
/// </summary>
public class LeadRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

    private static Lead NewLead(LeadStatus status = LeadStatus.New, Guid? ownerId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            LeadNo = "LD202610060001",
            Name = "甲线索",
            Contact = "张经理",
            Phone = "13800000000",
            Source = LeadSource.Exhibition,
            Status = status,
            OwnerId = ownerId,
            Remark = "备注",
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

    private static CreateLeadRequestHandler NewCreateHandler(
        AppDbContext context, FakeLeadRepository leads, RecordingAuditLogger audit)
        => new(leads, new EmployeeRepository(context), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

    private static UpdateLeadRequestHandler NewUpdateHandler(
        AppDbContext context, FakeLeadRepository leads, RecordingAuditLogger audit)
        => new(leads, new EmployeeRepository(context), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

    // ============================== 新增 ==============================

    [Fact]
    public async Task 新增线索_应落库来源与状态并生成LD单号()
    {
        using var context = TestSupport.CreateDbContext();
        var employee = NewEmployee(context);
        var leads = new FakeLeadRepository();
        var audit = new RecordingAuditLogger();

        var result = await NewCreateHandler(context, leads, audit).HandleAsync(new CreateLeadRequest
        {
            Name = "  乙线索  ",
            Contact = "  王经理  ",
            Phone = " 13900000000 ",
            Source = LeadSource.Website,
            Status = LeadStatus.Following,
            OwnerId = employee.Id,
            Remark = "  展会跟进  ",
        });

        Assert.StartsWith("LD", result.LeadNo);
        Assert.Equal("乙线索", result.Name);
        Assert.Equal("王经理", result.Contact);
        Assert.Equal("13900000000", result.Phone);
        Assert.Equal((int)LeadSource.Website, result.Source);
        Assert.Equal((int)LeadStatus.Following, result.Status);
        Assert.Equal(employee.Id.ToString(), result.OwnerId);
        Assert.Equal("展会跟进", result.Remark);
        Assert.Null(result.OpportunityId);
        Assert.Equal(new[] { "LD" }, leads.GeneratedPrefixes);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Lead, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Equal(result.LeadNo, entry.ResourceNo);
    }

    [Fact]
    public async Task 新增线索_负责人不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewCreateHandler(context, new FakeLeadRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateLeadRequest
        {
            Name = "乙线索",
            OwnerId = Guid.NewGuid(),
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑（全量覆盖）==============================

    [Fact]
    public async Task 编辑线索_空白可空字段应清空且状态可流转至跟进中()
    {
        using var context = TestSupport.CreateDbContext();
        var leads = new FakeLeadRepository();
        var lead = NewLead();
        leads.Seed(lead);
        var audit = new RecordingAuditLogger();

        var result = await NewUpdateHandler(context, leads, audit).HandleAsync(new UpdateLeadRequest
        {
            Id = lead.Id,
            Name = "甲线索改名",
            Contact = "   ",
            Phone = null,
            Source = LeadSource.Referral,
            Status = LeadStatus.Following,
            Remark = null,
        });

        Assert.Equal("甲线索改名", result.Name);
        Assert.Null(result.Contact);
        Assert.Null(result.Phone);
        Assert.Null(result.Remark);
        Assert.Equal((int)LeadSource.Referral, result.Source);
        Assert.Equal((int)LeadStatus.Following, result.Status);
        Assert.Equal(AuditAction.Update, Assert.Single(audit.Entries).Action);
    }

    [Theory]
    [InlineData(LeadStatus.Converted)]
    [InlineData(LeadStatus.Abandoned)]
    public async Task 编辑线索_终态_应报LeadNotConvertible(LeadStatus status)
    {
        using var context = TestSupport.CreateDbContext();
        var leads = new FakeLeadRepository();
        var lead = NewLead(status);
        leads.Seed(lead);

        var handler = NewUpdateHandler(context, leads, new RecordingAuditLogger());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateLeadRequest
        {
            Id = lead.Id,
            Name = "甲线索改名",
            Status = LeadStatus.Following,
        }));

        Assert.Equal(ErrorCode.LeadNotConvertible, ex.Code);
    }

    [Fact]
    public async Task 编辑线索_目标状态为已转化_应报LeadNotConvertible()
    {
        using var context = TestSupport.CreateDbContext();
        var leads = new FakeLeadRepository();
        var lead = NewLead();
        leads.Seed(lead);

        var handler = NewUpdateHandler(context, leads, new RecordingAuditLogger());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateLeadRequest
        {
            Id = lead.Id,
            Name = "甲线索改名",
            Status = LeadStatus.Converted,
        }));

        Assert.Equal(ErrorCode.LeadNotConvertible, ex.Code);
    }

    [Fact]
    public async Task 编辑线索_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewUpdateHandler(context, new FakeLeadRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateLeadRequest
        {
            Id = Guid.NewGuid(),
            Name = "甲线索",
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 状态流转 ==============================

    [Fact]
    public async Task 状态流转_新线索废弃_应落库并写状态变更日志()
    {
        using var context = TestSupport.CreateDbContext();
        var leads = new FakeLeadRepository();
        var lead = NewLead();
        leads.Seed(lead);
        var audit = new RecordingAuditLogger();

        var handler = new UpdateLeadStatusRequestHandler(
            leads, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new UpdateLeadStatusRequest { Id = lead.Id, Status = LeadStatus.Abandoned });

        Assert.Equal((int)LeadStatus.Abandoned, result.Status);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Lead, entry.Resource);
        Assert.Equal(AuditAction.StatusChange, entry.Action);
    }

    [Theory]
    [InlineData(LeadStatus.Converted)]
    [InlineData(LeadStatus.Abandoned)]
    public async Task 状态流转_终态线索_应报LeadNotConvertible(LeadStatus status)
    {
        using var context = TestSupport.CreateDbContext();
        var leads = new FakeLeadRepository();
        var lead = NewLead(status);
        leads.Seed(lead);

        var handler = new UpdateLeadStatusRequestHandler(
            leads, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new UpdateLeadStatusRequest { Id = lead.Id, Status = LeadStatus.Following }));

        Assert.Equal(ErrorCode.LeadNotConvertible, ex.Code);
    }

    [Fact]
    public async Task 状态流转_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = new UpdateLeadStatusRequestHandler(
            new FakeLeadRepository(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new UpdateLeadStatusRequest { Id = Guid.NewGuid(), Status = LeadStatus.Following }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 转商机 ==============================

    [Fact]
    public async Task 转商机_应同一事务创建商机并回写线索()
    {
        using var context = TestSupport.CreateDbContext();
        var calls = new List<string>();
        var ownerId = Guid.NewGuid();
        var lead = NewLead(LeadStatus.Following, ownerId);
        var leads = new FakeLeadRepository(calls);
        leads.Seed(lead);
        var opportunities = new FakeOpportunityRepository(calls);
        var audit = new RecordingAuditLogger();

        var handler = new ConvertLeadRequestHandler(
            leads, opportunities, new RecordingUnitOfWork(calls), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new ConvertLeadRequest { Id = lead.Id });

        // 商机：名称取线索名、客户空、负责人同线索、阶段初步接洽、金额 0
        Assert.StartsWith("OP", result.OpportunityNo);
        var opportunity = Assert.Single(opportunities.Opportunities);
        Assert.Equal(lead.Name, opportunity.Name);
        Assert.Equal(lead.Id, opportunity.LeadId);
        Assert.Null(opportunity.PartnerId);
        Assert.Null(opportunity.PartnerName);
        Assert.Equal(0m, opportunity.Amount);
        Assert.Equal(OpportunityStage.Initial, opportunity.Stage);
        Assert.Equal(ownerId, opportunity.OwnerId);

        // 线索：置已转化（终态）并回填商机 id / 单号
        Assert.Equal((int)LeadStatus.Converted, (int)lead.Status);
        Assert.Equal(opportunity.Id, lead.OpportunityId);
        Assert.Equal(opportunity.OpportunityNo, lead.OpportunityNo);

        // 同一事务：Begin → 生成单号 → 建商机 → 回写线索 → Commit（顺序即原子性证据）
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Update", "Commit" }, calls);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Lead, entry.Resource);
        Assert.Equal(AuditAction.Update, entry.Action);
    }

    [Theory]
    [InlineData(LeadStatus.Converted)]
    [InlineData(LeadStatus.Abandoned)]
    public async Task 转商机_终态线索_应报LeadNotConvertible(LeadStatus status)
    {
        using var context = TestSupport.CreateDbContext();
        var lead = NewLead(status);
        var leads = new FakeLeadRepository();
        leads.Seed(lead);
        var opportunities = new FakeOpportunityRepository();

        var handler = new ConvertLeadRequestHandler(
            leads, opportunities, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new ConvertLeadRequest { Id = lead.Id }));

        Assert.Equal(ErrorCode.LeadNotConvertible, ex.Code);
        Assert.Empty(opportunities.Opportunities);
    }

    [Fact]
    public async Task 转商机_线索不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = new ConvertLeadRequestHandler(
            new FakeLeadRepository(), new FakeOpportunityRepository(), new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ConvertLeadRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 列表与详情 ==============================

    [Fact]
    public async Task 线索列表_筛选条件应透传仓储且带出负责人姓名()
    {
        var leads = new FakeLeadRepository();
        var ownerId = Guid.NewGuid();
        var lead = NewLead(ownerId: ownerId);
        leads.OwnerNames[ownerId] = "李负责";
        IReadOnlyList<LeadListItem> items = new[] { new LeadListItem { Lead = lead, OwnerName = "李负责" } };
        leads.PagedResult = (items, 1);

        var handler = new GetLeadsRequestHandler(leads);
        var result = await handler.HandleAsync(new GetLeadsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "甲",
            Source = LeadSource.Exhibition,
            Status = LeadStatus.New,
            OwnerId = ownerId,
        });

        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal("李负责", result.Items[0].OwnerName);

        var args = leads.LastPagedArgs!.Value;
        Assert.Equal("甲", args.Keyword);
        Assert.Equal(LeadSource.Exhibition, args.Source);
        Assert.Equal(LeadStatus.New, args.Status);
        Assert.Equal(ownerId, args.OwnerId);
        Assert.Equal(2, args.Page);
        Assert.Equal(10, args.PageSize);
    }

    [Fact]
    public async Task 线索详情_不存在_应报40400()
    {
        var handler = new GetLeadByIdRequestHandler(new FakeLeadRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetLeadByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
