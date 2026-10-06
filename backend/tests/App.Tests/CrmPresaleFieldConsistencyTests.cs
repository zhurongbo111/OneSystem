using App.Core.Entities;
using App.Core.Features.Activities.CreateLeadActivity;
using App.Core.Features.Activities.CreateOpportunityActivity;
using App.Core.Features.Leads.CreateLead;
using App.Core.Features.Leads.GetLeads;
using App.Core.Features.Leads.UpdateLead;
using App.Core.Features.Opportunities.CreateOpportunity;
using App.Core.Features.Opportunities.GetOpportunities;
using App.Core.Features.Opportunities.UpdateOpportunity;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// CRM 售前（erp-crm-presale）字段约束一致性测试：
/// ① EF 实际列长 == 三个字段约束常量类；
/// ② 同一字段在创建 / 编辑两处 Validator 中边界一致；
/// ③ 查询关键词上限 == 列表模糊匹配列中的最大列长；
/// ④ 枚举筛选合法取值通过、非法取值拒绝。
/// </summary>
public class CrmPresaleFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_线索列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(LeadFieldConstraints.NoMaxLength, GetMaxLength<Lead>(dbContext, nameof(Lead.LeadNo)));
        Assert.Equal(LeadFieldConstraints.NoMaxLength, GetMaxLength<Lead>(dbContext, nameof(Lead.OpportunityNo)));
        Assert.Equal(LeadFieldConstraints.NameMaxLength, GetMaxLength<Lead>(dbContext, nameof(Lead.Name)));
        Assert.Equal(LeadFieldConstraints.ContactMaxLength, GetMaxLength<Lead>(dbContext, nameof(Lead.Contact)));
        Assert.Equal(LeadFieldConstraints.PhoneMaxLength, GetMaxLength<Lead>(dbContext, nameof(Lead.Phone)));
        Assert.Equal(LeadFieldConstraints.RemarkMaxLength, GetMaxLength<Lead>(dbContext, nameof(Lead.Remark)));
    }

    [Fact]
    public void EF模型_商机列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OpportunityFieldConstraints.NoMaxLength, GetMaxLength<Opportunity>(dbContext, nameof(Opportunity.OpportunityNo)));
        Assert.Equal(OpportunityFieldConstraints.NameMaxLength, GetMaxLength<Opportunity>(dbContext, nameof(Opportunity.Name)));
        Assert.Equal(OpportunityFieldConstraints.RemarkMaxLength, GetMaxLength<Opportunity>(dbContext, nameof(Opportunity.Remark)));
        Assert.Equal(PartnerFieldConstraints.NameMaxLength, GetMaxLength<Opportunity>(dbContext, nameof(Opportunity.PartnerName)));
    }

    [Fact]
    public void EF模型_跟进活动列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(ActivityFieldConstraints.ContentMaxLength, GetMaxLength<Activity>(dbContext, nameof(Activity.Content)));
    }

    // ============================== 线索：创建 / 编辑边界一致 ==============================

    private static CreateLeadRequest NewCreateLead(string name = "线索", string? contact = null, string? phone = null, string? remark = null)
        => new() { Name = name, Contact = contact, Phone = phone, Remark = remark };

    private static UpdateLeadRequest NewUpdateLead(string name = "线索", string? contact = null, string? phone = null, string? remark = null)
        => new() { Id = Guid.NewGuid(), Name = name, Contact = contact, Phone = phone, Remark = remark };

    [Fact]
    public void 线索名称长度边界_创建与编辑两处应一致()
    {
        var createValidator = new CreateLeadRequestValidator();
        var updateValidator = new UpdateLeadRequestValidator();

        Assert.False(createValidator.Validate(NewCreateLead(name: string.Empty)).IsValid);
        Assert.True(createValidator.Validate(NewCreateLead(name: new string('a', LeadFieldConstraints.NameMaxLength))).IsValid);
        Assert.False(createValidator.Validate(NewCreateLead(name: new string('a', LeadFieldConstraints.NameMaxLength + 1))).IsValid);

        Assert.False(updateValidator.Validate(NewUpdateLead(name: string.Empty)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdateLead(name: new string('a', LeadFieldConstraints.NameMaxLength))).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateLead(name: new string('a', LeadFieldConstraints.NameMaxLength + 1))).IsValid);
    }

    [Fact]
    public void 线索联系人手机备注长度边界_创建与编辑两处应一致()
    {
        var createValidator = new CreateLeadRequestValidator();
        var updateValidator = new UpdateLeadRequestValidator();

        var contactOk = new string('c', LeadFieldConstraints.ContactMaxLength);
        var contactTooLong = new string('c', LeadFieldConstraints.ContactMaxLength + 1);
        var phoneOk = new string('1', LeadFieldConstraints.PhoneMaxLength);
        var phoneTooLong = new string('1', LeadFieldConstraints.PhoneMaxLength + 1);
        var remarkOk = new string('r', LeadFieldConstraints.RemarkMaxLength);
        var remarkTooLong = new string('r', LeadFieldConstraints.RemarkMaxLength + 1);

        Assert.True(createValidator.Validate(NewCreateLead(contact: contactOk, phone: phoneOk, remark: remarkOk)).IsValid);
        Assert.False(createValidator.Validate(NewCreateLead(contact: contactTooLong)).IsValid);
        Assert.False(createValidator.Validate(NewCreateLead(phone: phoneTooLong)).IsValid);
        Assert.False(createValidator.Validate(NewCreateLead(remark: remarkTooLong)).IsValid);

        Assert.True(updateValidator.Validate(NewUpdateLead(contact: contactOk, phone: phoneOk, remark: remarkOk)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateLead(contact: contactTooLong)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateLead(phone: phoneTooLong)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateLead(remark: remarkTooLong)).IsValid);
    }

    [Fact]
    public void 新建线索状态_只能是新线索或跟进中()
    {
        var validator = new CreateLeadRequestValidator();

        Assert.True(validator.Validate(new CreateLeadRequest { Name = "线索", Status = LeadStatus.New }).IsValid);
        Assert.True(validator.Validate(new CreateLeadRequest { Name = "线索", Status = LeadStatus.Following }).IsValid);
        Assert.False(validator.Validate(new CreateLeadRequest { Name = "线索", Status = LeadStatus.Converted }).IsValid);
        Assert.False(validator.Validate(new CreateLeadRequest { Name = "线索", Status = LeadStatus.Abandoned }).IsValid);
        Assert.False(validator.Validate(new CreateLeadRequest { Name = "线索", Status = (LeadStatus)99 }).IsValid);
    }

    // ============================== 商机：创建 / 编辑边界一致 ==============================

    private static CreateOpportunityRequest NewCreateOpportunity(string name = "商机", decimal amount = 0m, string? remark = null)
        => new() { Name = name, Amount = amount, Remark = remark };

    private static UpdateOpportunityRequest NewUpdateOpportunity(string name = "商机", decimal amount = 0m, string? remark = null)
        => new() { Id = Guid.NewGuid(), Name = name, Amount = amount, Remark = remark };

    [Fact]
    public void 商机名称与备注长度边界_创建与编辑两处应一致()
    {
        var createValidator = new CreateOpportunityRequestValidator();
        var updateValidator = new UpdateOpportunityRequestValidator();

        Assert.True(createValidator.Validate(NewCreateOpportunity(new string('a', OpportunityFieldConstraints.NameMaxLength))).IsValid);
        Assert.False(createValidator.Validate(NewCreateOpportunity(string.Empty)).IsValid);
        Assert.False(createValidator.Validate(NewCreateOpportunity(new string('a', OpportunityFieldConstraints.NameMaxLength + 1))).IsValid);
        Assert.False(createValidator.Validate(NewCreateOpportunity(remark: new string('r', OpportunityFieldConstraints.RemarkMaxLength + 1))).IsValid);

        Assert.True(updateValidator.Validate(NewUpdateOpportunity(new string('a', OpportunityFieldConstraints.NameMaxLength))).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateOpportunity(string.Empty)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateOpportunity(new string('a', OpportunityFieldConstraints.NameMaxLength + 1))).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateOpportunity(remark: new string('r', OpportunityFieldConstraints.RemarkMaxLength + 1))).IsValid);
    }

    [Fact]
    public void 商机金额边界_创建与编辑两处应一致()
    {
        var createValidator = new CreateOpportunityRequestValidator();
        var updateValidator = new UpdateOpportunityRequestValidator();

        Assert.True(createValidator.Validate(NewCreateOpportunity(amount: 0m)).IsValid);
        Assert.True(createValidator.Validate(NewCreateOpportunity(amount: OpportunityFieldConstraints.AmountMaxValue)).IsValid);
        Assert.False(createValidator.Validate(NewCreateOpportunity(amount: -0.01m)).IsValid);
        Assert.False(createValidator.Validate(NewCreateOpportunity(amount: OpportunityFieldConstraints.AmountMaxValue + 1m)).IsValid);

        Assert.True(updateValidator.Validate(NewUpdateOpportunity(amount: 0m)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdateOpportunity(amount: OpportunityFieldConstraints.AmountMaxValue)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateOpportunity(amount: -0.01m)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateOpportunity(amount: OpportunityFieldConstraints.AmountMaxValue + 1m)).IsValid);
    }

    [Fact]
    public void 商机阶段取值_合法枚举通过且非法拒绝()
    {
        var createValidator = new CreateOpportunityRequestValidator();
        var updateValidator = new UpdateOpportunityRequestValidator();

        foreach (var stage in Enum.GetValues<OpportunityStage>())
        {
            Assert.True(createValidator.Validate(new CreateOpportunityRequest { Name = "商机", Stage = stage }).IsValid);
            Assert.True(updateValidator.Validate(new UpdateOpportunityRequest { Id = Guid.NewGuid(), Name = "商机", Stage = stage }).IsValid);
        }

        Assert.False(createValidator.Validate(new CreateOpportunityRequest { Name = "商机", Stage = (OpportunityStage)99 }).IsValid);
        Assert.False(updateValidator.Validate(new UpdateOpportunityRequest { Id = Guid.NewGuid(), Name = "商机", Stage = (OpportunityStage)99 }).IsValid);
    }

    // ============================== 查询关键词 ←→ 匹配列长 / 枚举筛选 ==============================

    [Fact]
    public void 线索与商机查询关键词长度_应不超过各自匹配列的最大列长()
    {
        var leadsValidator = new GetLeadsRequestValidator();
        var opportunitiesValidator = new GetOpportunitiesRequestValidator();

        var leadOk = new string('a', LeadFieldConstraints.KeywordMaxLength);
        var leadTooLong = new string('a', LeadFieldConstraints.KeywordMaxLength + 1);
        var opportunityOk = new string('a', OpportunityFieldConstraints.KeywordMaxLength);
        var opportunityTooLong = new string('a', OpportunityFieldConstraints.KeywordMaxLength + 1);

        Assert.True(leadsValidator.Validate(new GetLeadsRequest { Keyword = leadOk }).IsValid);
        Assert.False(leadsValidator.Validate(new GetLeadsRequest { Keyword = leadTooLong }).IsValid);
        Assert.True(opportunitiesValidator.Validate(new GetOpportunitiesRequest { Keyword = opportunityOk }).IsValid);
        Assert.False(opportunitiesValidator.Validate(new GetOpportunitiesRequest { Keyword = opportunityTooLong }).IsValid);

        // 关键词上限即名称列长（列表模糊匹配列中的最大列长）
        Assert.Equal(LeadFieldConstraints.NameMaxLength, LeadFieldConstraints.KeywordMaxLength);
        Assert.Equal(OpportunityFieldConstraints.NameMaxLength, OpportunityFieldConstraints.KeywordMaxLength);
    }

    [Fact]
    public void 线索列表来源与状态筛选_非法取值应被拒绝()
    {
        var validator = new GetLeadsRequestValidator();

        foreach (var source in Enum.GetValues<LeadSource>())
        {
            Assert.True(validator.Validate(new GetLeadsRequest { Source = source }).IsValid);
        }

        foreach (var status in Enum.GetValues<LeadStatus>())
        {
            Assert.True(validator.Validate(new GetLeadsRequest { Status = status }).IsValid);
        }

        Assert.False(validator.Validate(new GetLeadsRequest { Source = (LeadSource)99 }).IsValid);
        Assert.False(validator.Validate(new GetLeadsRequest { Status = (LeadStatus)99 }).IsValid);
    }

    [Fact]
    public void 商机列表阶段筛选_非法取值应被拒绝()
    {
        var validator = new GetOpportunitiesRequestValidator();

        foreach (var stage in Enum.GetValues<OpportunityStage>())
        {
            Assert.True(validator.Validate(new GetOpportunitiesRequest { Stage = stage }).IsValid);
        }

        Assert.False(validator.Validate(new GetOpportunitiesRequest { Stage = (OpportunityStage)99 }).IsValid);
    }

    [Fact]
    public void 分页边界_线索与商机查询均应一致()
    {
        var leadsValidator = new GetLeadsRequestValidator();
        var opportunitiesValidator = new GetOpportunitiesRequestValidator();

        Assert.False(leadsValidator.Validate(new GetLeadsRequest { Page = 0 }).IsValid);
        Assert.False(leadsValidator.Validate(new GetLeadsRequest { PageSize = 101 }).IsValid);
        Assert.True(leadsValidator.Validate(new GetLeadsRequest { Page = 1, PageSize = 100 }).IsValid);

        Assert.False(opportunitiesValidator.Validate(new GetOpportunitiesRequest { Page = 0 }).IsValid);
        Assert.False(opportunitiesValidator.Validate(new GetOpportunitiesRequest { PageSize = 101 }).IsValid);
        Assert.True(opportunitiesValidator.Validate(new GetOpportunitiesRequest { Page = 1, PageSize = 100 }).IsValid);
    }

    // ============================== 跟进活动：内容与跟进时间 ==============================

    [Fact]
    public void 跟进活动内容与时间_线索与商机两处应一致()
    {
        var leadValidator = new CreateLeadActivityRequestValidator();
        var opportunityValidator = new CreateOpportunityActivityRequestValidator();
        var now = DateTimeOffset.UtcNow;

        var contentOk = new string('c', ActivityFieldConstraints.ContentMaxLength);
        var contentTooLong = new string('c', ActivityFieldConstraints.ContentMaxLength + 1);

        Assert.True(leadValidator.Validate(new CreateLeadActivityRequest { LeadId = Guid.NewGuid(), Content = contentOk, ActivityTime = now }).IsValid);
        Assert.False(leadValidator.Validate(new CreateLeadActivityRequest { LeadId = Guid.NewGuid(), Content = contentTooLong, ActivityTime = now }).IsValid);
        Assert.False(leadValidator.Validate(new CreateLeadActivityRequest { LeadId = Guid.NewGuid(), Content = " ", ActivityTime = now }).IsValid);
        Assert.False(leadValidator.Validate(new CreateLeadActivityRequest { LeadId = Guid.NewGuid(), Content = "电话跟进" }).IsValid);
        Assert.False(leadValidator.Validate(new CreateLeadActivityRequest { LeadId = Guid.NewGuid(), Content = "电话跟进", ActivityTime = now, Type = (ActivityType)99 }).IsValid);

        Assert.True(opportunityValidator.Validate(new CreateOpportunityActivityRequest { OpportunityId = Guid.NewGuid(), Content = contentOk, ActivityTime = now }).IsValid);
        Assert.False(opportunityValidator.Validate(new CreateOpportunityActivityRequest { OpportunityId = Guid.NewGuid(), Content = contentTooLong, ActivityTime = now }).IsValid);
        Assert.False(opportunityValidator.Validate(new CreateOpportunityActivityRequest { OpportunityId = Guid.NewGuid(), Content = "电话跟进" }).IsValid);
        Assert.False(opportunityValidator.Validate(new CreateOpportunityActivityRequest { OpportunityId = Guid.NewGuid(), Content = "电话跟进", ActivityTime = now, Type = (ActivityType)99 }).IsValid);
    }
}
