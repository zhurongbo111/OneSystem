using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.ServiceTickets.AssignServiceTicket;
using App.Core.Features.ServiceTickets.CreateServiceTicket;
using App.Core.Features.ServiceTickets.GetServiceTicketById;
using App.Core.Features.ServiceTickets.GetServiceTickets;
using App.Core.Features.ServiceTickets.UpdateServiceTicket;
using App.Core.Features.ServiceTickets.UpdateServiceTicketStatus;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 服务工单用例测试（specs/045-erp-crm-service design.md §6）：
/// 单号与落库字段、客户 / 负责人存在性、已关闭锁定（40172）、状态流转白名单与解决时间、指派、
/// 筛选透传与审计写入。
/// </summary>
public class ServiceTicketRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    private static ServiceTicket NewTicket(
        TicketStatus status = TicketStatus.Pending,
        TicketPriority priority = TicketPriority.Medium,
        Guid? ownerId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TicketNo = "SV202610070001",
            PartnerId = Guid.NewGuid(),
            PartnerName = "甲客户",
            Contact = "张经理",
            Phone = "13800000000",
            Title = "设备异响",
            Description = "运行时有异响",
            Priority = priority,
            Status = status,
            OwnerId = ownerId,
            Remark = "备注",
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static Partner NewPartner(AppDbContext context, string name = "甲客户")
    {
        var partner = TestSupport.NewPartner(name, PartnerType.Customer);
        context.Partners.Add(partner);
        context.SaveChanges();
        return partner;
    }

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

    private static CreateServiceTicketRequestHandler NewCreateHandler(
        AppDbContext context, FakeServiceTicketRepository tickets, RecordingAuditLogger audit)
        => new(
            tickets,
            new PartnerRepository(context),
            new EmployeeRepository(context),
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            audit);

    private static UpdateServiceTicketRequestHandler NewUpdateHandler(
        AppDbContext context, FakeServiceTicketRepository tickets, RecordingAuditLogger audit)
        => new(
            tickets,
            new PartnerRepository(context),
            new EmployeeRepository(context),
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            audit);

    // ============================== 登记 ==============================

    [Fact]
    public async Task 登记工单_应落库客户快照与优先级并生成SV单号()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = NewPartner(context);
        var employee = NewEmployee(context);
        var tickets = new FakeServiceTicketRepository();
        var audit = new RecordingAuditLogger();

        var result = await NewCreateHandler(context, tickets, audit).HandleAsync(new CreateServiceTicketRequest
        {
            PartnerId = partner.Id,
            Title = "  设备异响  ",
            Contact = "  王经理  ",
            Phone = " 13900000000 ",
            Description = "  运行时有异响  ",
            Priority = TicketPriority.High,
            OwnerId = employee.Id,
            Remark = "  加急  ",
        });

        Assert.StartsWith("SV", result.TicketNo);
        Assert.Equal(partner.Id.ToString(), result.PartnerId);
        Assert.Equal("甲客户", result.PartnerName);
        Assert.Equal("设备异响", result.Title);
        Assert.Equal("王经理", result.Contact);
        Assert.Equal("13900000000", result.Phone);
        Assert.Equal("运行时有异响", result.Description);
        Assert.Equal((int)TicketPriority.High, result.Priority);
        Assert.Equal((int)TicketStatus.Pending, result.Status);
        Assert.Equal(employee.Id.ToString(), result.OwnerId);
        Assert.Null(result.ResolvedAt);
        Assert.Equal("加急", result.Remark);
        Assert.Equal(new[] { "SV" }, tickets.GeneratedPrefixes);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.ServiceTicket, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Equal(result.TicketNo, entry.ResourceNo);
    }

    [Fact]
    public async Task 登记工单_客户不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewCreateHandler(context, new FakeServiceTicketRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateServiceTicketRequest
        {
            PartnerId = Guid.NewGuid(),
            Title = "设备异响",
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 登记工单_负责人不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = NewPartner(context);
        var handler = NewCreateHandler(context, new FakeServiceTicketRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateServiceTicketRequest
        {
            PartnerId = partner.Id,
            Title = "设备异响",
            OwnerId = Guid.NewGuid(),
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑（全量覆盖）==============================

    [Fact]
    public async Task 编辑工单_空白可空字段应清空并刷新客户快照()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = NewPartner(context, "乙客户");
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket();
        tickets.Seed(ticket);
        var audit = new RecordingAuditLogger();

        var result = await NewUpdateHandler(context, tickets, audit).HandleAsync(new UpdateServiceTicketRequest
        {
            Id = ticket.Id,
            PartnerId = partner.Id,
            Title = "设备异响（已复现）",
            Contact = "   ",
            Phone = null,
            Description = null,
            Priority = TicketPriority.Low,
            Remark = null,
        });

        Assert.Equal("乙客户", result.PartnerName);
        Assert.Equal("设备异响（已复现）", result.Title);
        Assert.Null(result.Contact);
        Assert.Null(result.Phone);
        Assert.Null(result.Description);
        Assert.Equal((int)TicketPriority.Low, result.Priority);
        Assert.Null(result.Remark);
        Assert.Equal(AuditAction.Update, Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task 编辑工单_已关闭_应报TicketStateInvalid()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = NewPartner(context);
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket(TicketStatus.Closed);
        tickets.Seed(ticket);

        var handler = NewUpdateHandler(context, tickets, new RecordingAuditLogger());
        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateServiceTicketRequest
        {
            Id = ticket.Id,
            PartnerId = partner.Id,
            Title = "设备异响",
        }));

        Assert.Equal(ErrorCode.TicketStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 编辑工单_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = NewUpdateHandler(context, new FakeServiceTicketRepository(), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateServiceTicketRequest
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            Title = "设备异响",
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 状态流转 ==============================

    [Theory]
    [InlineData(TicketStatus.Pending, TicketStatus.Processing)]
    [InlineData(TicketStatus.Pending, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Pending, TicketStatus.Closed)]
    [InlineData(TicketStatus.Processing, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Processing, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Processing)]
    public async Task 状态流转_白名单内_应落库并写状态变更日志(TicketStatus from, TicketStatus to)
    {
        using var context = TestSupport.CreateDbContext();
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket(from);
        tickets.Seed(ticket);
        var audit = new RecordingAuditLogger();

        var handler = new UpdateServiceTicketStatusRequestHandler(
            tickets, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new UpdateServiceTicketStatusRequest { Id = ticket.Id, Status = to });

        Assert.Equal((int)to, result.Status);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.ServiceTicket, entry.Resource);
        Assert.Equal(AuditAction.StatusChange, entry.Action);

        // 置「已解决」记录解决时间；重开清空；其余流转不动解决时间
        if (to == TicketStatus.Resolved)
        {
            Assert.NotNull(result.ResolvedAt);
        }
        else if (from == TicketStatus.Resolved && to == TicketStatus.Processing)
        {
            Assert.Null(result.ResolvedAt);
        }
    }

    [Theory]
    [InlineData(TicketStatus.Pending, TicketStatus.Pending)]
    [InlineData(TicketStatus.Processing, TicketStatus.Pending)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed, TicketStatus.Pending)]
    [InlineData(TicketStatus.Closed, TicketStatus.Processing)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed, TicketStatus.Closed)]
    public async Task 状态流转_白名单外或已关闭_应报TicketStateInvalid(TicketStatus from, TicketStatus to)
    {
        using var context = TestSupport.CreateDbContext();
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket(from);
        tickets.Seed(ticket);

        var handler = new UpdateServiceTicketStatusRequestHandler(
            tickets, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new UpdateServiceTicketStatusRequest { Id = ticket.Id, Status = to }));

        Assert.Equal(ErrorCode.TicketStateInvalid, ex.Code);
        Assert.Equal(from, ticket.Status);
    }

    [Fact]
    public async Task 状态流转_已解决重开_应清空解决时间()
    {
        using var context = TestSupport.CreateDbContext();
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket(TicketStatus.Resolved);
        ticket.ResolvedAt = Now;
        tickets.Seed(ticket);

        var handler = new UpdateServiceTicketStatusRequestHandler(
            tickets, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var result = await handler.HandleAsync(
            new UpdateServiceTicketStatusRequest { Id = ticket.Id, Status = TicketStatus.Processing });

        Assert.Equal((int)TicketStatus.Processing, result.Status);
        Assert.Null(result.ResolvedAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public async Task 状态流转_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = new UpdateServiceTicketStatusRequestHandler(
            new FakeServiceTicketRepository(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new UpdateServiceTicketStatusRequest { Id = Guid.NewGuid(), Status = TicketStatus.Processing }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 指派 ==============================

    [Fact]
    public async Task 指派负责人_应回写并写更新日志()
    {
        using var context = TestSupport.CreateDbContext();
        var employee = NewEmployee(context, "王负责");
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket();
        tickets.Seed(ticket);
        var audit = new RecordingAuditLogger();

        var handler = new AssignServiceTicketRequestHandler(
            tickets, new EmployeeRepository(context), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new AssignServiceTicketRequest { Id = ticket.Id, OwnerId = employee.Id });

        Assert.Equal(employee.Id.ToString(), result.OwnerId);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.ServiceTicket, entry.Resource);
        Assert.Equal(AuditAction.Update, entry.Action);
    }

    [Fact]
    public async Task 指派负责人_已关闭_应报TicketStateInvalid()
    {
        using var context = TestSupport.CreateDbContext();
        var employee = NewEmployee(context);
        var tickets = new FakeServiceTicketRepository();
        tickets.Seed(NewTicket(TicketStatus.Closed));

        var handler = new AssignServiceTicketRequestHandler(
            tickets, new EmployeeRepository(context), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new AssignServiceTicketRequest { Id = tickets.Tickets.First().Id, OwnerId = employee.Id }));

        Assert.Equal(ErrorCode.TicketStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 指派负责人_负责人不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var tickets = new FakeServiceTicketRepository();
        var ticket = NewTicket();
        tickets.Seed(ticket);

        var handler = new AssignServiceTicketRequestHandler(
            tickets, new EmployeeRepository(context), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new AssignServiceTicketRequest { Id = ticket.Id, OwnerId = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 指派负责人_工单不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var employee = NewEmployee(context);

        var handler = new AssignServiceTicketRequestHandler(
            new FakeServiceTicketRepository(), new EmployeeRepository(context), new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new AssignServiceTicketRequest { Id = Guid.NewGuid(), OwnerId = employee.Id }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 列表与详情 ==============================

    [Fact]
    public async Task 工单列表_筛选条件应透传仓储且带出负责人姓名()
    {
        var tickets = new FakeServiceTicketRepository();
        var ownerId = Guid.NewGuid();
        var ticket = NewTicket(TicketStatus.Processing, TicketPriority.High, ownerId);
        IReadOnlyList<ServiceTicketListItem> items = [new ServiceTicketListItem { Ticket = ticket, OwnerName = "李负责" }];
        tickets.PagedResult = (items, 1);

        var handler = new GetServiceTicketsRequestHandler(tickets);
        var result = await handler.HandleAsync(new GetServiceTicketsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "甲客户",
            Status = TicketStatus.Processing,
            Priority = TicketPriority.High,
            OwnerId = ownerId,
        });

        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal("李负责", result.Items[0].OwnerName);
        Assert.Equal("甲客户", result.Items[0].PartnerName);
        Assert.Equal((int)TicketStatus.Processing, result.Items[0].Status);

        var args = tickets.LastPagedArgs!.Value;
        Assert.Equal("甲客户", args.Keyword);
        Assert.Equal(TicketStatus.Processing, args.Status);
        Assert.Equal(TicketPriority.High, args.Priority);
        Assert.Equal(ownerId, args.OwnerId);
        Assert.Equal(2, args.Page);
        Assert.Equal(10, args.PageSize);
    }

    [Fact]
    public async Task 工单详情_不存在_应报40400()
    {
        var handler = new GetServiceTicketByIdRequestHandler(new FakeServiceTicketRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetServiceTicketByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
