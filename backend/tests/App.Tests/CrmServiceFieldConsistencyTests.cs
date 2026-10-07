using App.Core.Entities;
using App.Core.Features.ServiceTickets.AssignServiceTicket;
using App.Core.Features.ServiceTickets.CreateServiceTicket;
using App.Core.Features.ServiceTickets.GetServiceTickets;
using App.Core.Features.ServiceTickets.UpdateServiceTicket;
using App.Core.Features.ServiceTickets.UpdateServiceTicketStatus;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// CRM 服务工单（erp-crm-service）字段约束一致性测试：
/// ① EF 实际列长 == 字段约束常量类（客户名称快照复用 <see cref="PartnerFieldConstraints"/>）；
/// ② 同一字段在登记 / 编辑两处 Validator 中边界一致；
/// ③ 查询关键词上限 == 列表模糊匹配列中的最大列长；
/// ④ 枚举（状态 / 优先级）合法取值通过、非法取值拒绝。
/// </summary>
public class CrmServiceFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_服务工单列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(ServiceTicketFieldConstraints.NoMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.TicketNo)));
        Assert.Equal(ServiceTicketFieldConstraints.TitleMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.Title)));
        Assert.Equal(ServiceTicketFieldConstraints.DescriptionMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.Description)));
        Assert.Equal(ServiceTicketFieldConstraints.ContactMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.Contact)));
        Assert.Equal(ServiceTicketFieldConstraints.PhoneMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.Phone)));
        Assert.Equal(ServiceTicketFieldConstraints.RemarkMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.Remark)));
        Assert.Equal(PartnerFieldConstraints.NameMaxLength, GetMaxLength<ServiceTicket>(dbContext, nameof(ServiceTicket.PartnerName)));
    }

    // ============================== 登记 / 编辑边界一致 ==============================

    private static CreateServiceTicketRequest NewCreate(
        string title = "设备异响", string? description = null, string? contact = null,
        string? phone = null, string? remark = null, Guid? partnerId = null)
        => new()
        {
            PartnerId = partnerId ?? Guid.NewGuid(),
            Title = title,
            Description = description,
            Contact = contact,
            Phone = phone,
            Remark = remark,
        };

    private static UpdateServiceTicketRequest NewUpdate(
        string title = "设备异响", string? description = null, string? contact = null,
        string? phone = null, string? remark = null, Guid? partnerId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            PartnerId = partnerId ?? Guid.NewGuid(),
            Title = title,
            Description = description,
            Contact = contact,
            Phone = phone,
            Remark = remark,
        };

    [Fact]
    public void 工单标题长度边界_登记与编辑两处应一致()
    {
        var createValidator = new CreateServiceTicketRequestValidator();
        var updateValidator = new UpdateServiceTicketRequestValidator();

        Assert.False(createValidator.Validate(NewCreate(title: string.Empty)).IsValid);
        Assert.True(createValidator.Validate(NewCreate(title: new string('a', ServiceTicketFieldConstraints.TitleMaxLength))).IsValid);
        Assert.False(createValidator.Validate(NewCreate(title: new string('a', ServiceTicketFieldConstraints.TitleMaxLength + 1))).IsValid);

        Assert.False(updateValidator.Validate(NewUpdate(title: string.Empty)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdate(title: new string('a', ServiceTicketFieldConstraints.TitleMaxLength))).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(title: new string('a', ServiceTicketFieldConstraints.TitleMaxLength + 1))).IsValid);
    }

    [Fact]
    public void 工单描述联系人电话备注长度边界_登记与编辑两处应一致()
    {
        var createValidator = new CreateServiceTicketRequestValidator();
        var updateValidator = new UpdateServiceTicketRequestValidator();

        var descriptionOk = new string('d', ServiceTicketFieldConstraints.DescriptionMaxLength);
        var descriptionTooLong = new string('d', ServiceTicketFieldConstraints.DescriptionMaxLength + 1);
        var contactOk = new string('c', ServiceTicketFieldConstraints.ContactMaxLength);
        var contactTooLong = new string('c', ServiceTicketFieldConstraints.ContactMaxLength + 1);
        var phoneOk = new string('1', ServiceTicketFieldConstraints.PhoneMaxLength);
        var phoneTooLong = new string('1', ServiceTicketFieldConstraints.PhoneMaxLength + 1);
        var remarkOk = new string('r', ServiceTicketFieldConstraints.RemarkMaxLength);
        var remarkTooLong = new string('r', ServiceTicketFieldConstraints.RemarkMaxLength + 1);

        Assert.True(createValidator.Validate(NewCreate(description: descriptionOk, contact: contactOk, phone: phoneOk, remark: remarkOk)).IsValid);
        Assert.False(createValidator.Validate(NewCreate(description: descriptionTooLong)).IsValid);
        Assert.False(createValidator.Validate(NewCreate(contact: contactTooLong)).IsValid);
        Assert.False(createValidator.Validate(NewCreate(phone: phoneTooLong)).IsValid);
        Assert.False(createValidator.Validate(NewCreate(remark: remarkTooLong)).IsValid);

        Assert.True(updateValidator.Validate(NewUpdate(description: descriptionOk, contact: contactOk, phone: phoneOk, remark: remarkOk)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(description: descriptionTooLong)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(contact: contactTooLong)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(phone: phoneTooLong)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(remark: remarkTooLong)).IsValid);
    }

    [Fact]
    public void 客户必填_登记与编辑两处应一致()
    {
        var createValidator = new CreateServiceTicketRequestValidator();
        var updateValidator = new UpdateServiceTicketRequestValidator();

        Assert.False(createValidator.Validate(NewCreate(partnerId: Guid.Empty)).IsValid);
        Assert.True(createValidator.Validate(NewCreate()).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(partnerId: Guid.Empty)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdate()).IsValid);
    }

    [Fact]
    public void 工单优先级取值_登记与编辑两处应一致()
    {
        var createValidator = new CreateServiceTicketRequestValidator();
        var updateValidator = new UpdateServiceTicketRequestValidator();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            var create = NewCreate();
            Assert.True(createValidator.Validate(new CreateServiceTicketRequest
            {
                PartnerId = create.PartnerId,
                Title = create.Title,
                Priority = priority,
            }).IsValid, $"优先级 {priority} 应被接受（登记）");

            var update = NewUpdate();
            Assert.True(updateValidator.Validate(new UpdateServiceTicketRequest
            {
                Id = update.Id,
                PartnerId = update.PartnerId,
                Title = update.Title,
                Priority = priority,
            }).IsValid, $"优先级 {priority} 应被接受（编辑）");
        }

        Assert.False(createValidator.Validate(new CreateServiceTicketRequest
        {
            PartnerId = Guid.NewGuid(),
            Title = "设备异响",
            Priority = (TicketPriority)99,
        }).IsValid);
        Assert.False(updateValidator.Validate(new UpdateServiceTicketRequest
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            Title = "设备异响",
            Priority = (TicketPriority)99,
        }).IsValid);
    }

    // ============================== 状态流转 / 指派 / 列表校验 ==============================

    [Fact]
    public void 状态流转请求_非法状态应被拒绝()
    {
        var validator = new UpdateServiceTicketStatusRequestValidator();

        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            Assert.True(validator.Validate(new UpdateServiceTicketStatusRequest { Id = Guid.NewGuid(), Status = status }).IsValid);
        }

        Assert.False(validator.Validate(new UpdateServiceTicketStatusRequest { Id = Guid.NewGuid(), Status = (TicketStatus)99 }).IsValid);
        Assert.False(validator.Validate(new UpdateServiceTicketStatusRequest { Id = Guid.Empty, Status = TicketStatus.Processing }).IsValid);
    }

    [Fact]
    public void 指派请求_负责人必填且工单id必填()
    {
        var validator = new AssignServiceTicketRequestValidator();

        Assert.True(validator.Validate(new AssignServiceTicketRequest { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() }).IsValid);
        Assert.False(validator.Validate(new AssignServiceTicketRequest { Id = Guid.NewGuid(), OwnerId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(new AssignServiceTicketRequest { Id = Guid.Empty, OwnerId = Guid.NewGuid() }).IsValid);
    }

    [Fact]
    public void 工单查询关键词长度_应不超过匹配列的最大列长()
    {
        var validator = new GetServiceTicketsRequestValidator();

        Assert.True(validator.Validate(new GetServiceTicketsRequest
        {
            Keyword = new string('a', ServiceTicketFieldConstraints.KeywordMaxLength),
        }).IsValid);
        Assert.False(validator.Validate(new GetServiceTicketsRequest
        {
            Keyword = new string('a', ServiceTicketFieldConstraints.KeywordMaxLength + 1),
        }).IsValid);

        // 关键词上限即标题列长（列表模糊匹配单号 / 客户名 / 标题中的最大列长）
        Assert.Equal(ServiceTicketFieldConstraints.TitleMaxLength, ServiceTicketFieldConstraints.KeywordMaxLength);
        Assert.Equal(PartnerFieldConstraints.NameMaxLength, ServiceTicketFieldConstraints.KeywordMaxLength);
        Assert.True(ServiceTicketFieldConstraints.KeywordMaxLength >= ServiceTicketFieldConstraints.NoMaxLength);
    }

    [Fact]
    public void 工单列表状态与优先级筛选_非法取值应被拒绝()
    {
        var validator = new GetServiceTicketsRequestValidator();

        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            Assert.True(validator.Validate(new GetServiceTicketsRequest { Status = status }).IsValid);
        }

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            Assert.True(validator.Validate(new GetServiceTicketsRequest { Priority = priority }).IsValid);
        }

        Assert.False(validator.Validate(new GetServiceTicketsRequest { Status = (TicketStatus)99 }).IsValid);
        Assert.False(validator.Validate(new GetServiceTicketsRequest { Priority = (TicketPriority)99 }).IsValid);
    }

    [Fact]
    public void 分页边界_工单查询应与其他域一致()
    {
        var validator = new GetServiceTicketsRequestValidator();

        Assert.False(validator.Validate(new GetServiceTicketsRequest { Page = 0 }).IsValid);
        Assert.False(validator.Validate(new GetServiceTicketsRequest { PageSize = 101 }).IsValid);
        Assert.True(validator.Validate(new GetServiceTicketsRequest { Page = 1, PageSize = 100 }).IsValid);
    }
}
