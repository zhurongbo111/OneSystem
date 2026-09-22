using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.AuditLogs.GetAuditLogById;
using App.Core.Features.AuditLogs.GetAuditLogs;
using App.Infrastructure;
using App.Infrastructure.Audit;
using App.Infrastructure.Repositories;

using Microsoft.Extensions.Logging.Abstractions;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Tests;

/// <summary>
/// 操作审计日志的写入基建与只读查询测试（specs/029-erp-audit-log tasks.md 4.1 / 4.5 / 4.7）：
/// `AuditChangeBuilder`（只记变化 / 敏感字段拦截 / 集合快照 / 截断 / JSON 形态）、
/// `AuditLogsController` 两个用例（筛选 / 倒序 / 分页 / 列表不加载差异 / 详情反序列化 / 空差异 / 坏 JSON / 40400）、
/// 字段约束（摘要 200 / 业务标识 50 / 关键词 50-51）。
/// </summary>
public class AuditLogInfrastructureTests
{
    private static readonly JsonSerializerOptions _changesReaderOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ===== 4.1 AuditChangeBuilder =====

    /// <summary>仅无变化的字段时差异为空（列存 NULL 而非空数组）</summary>
    [Fact]
    public void AuditChangeBuilder_NoChangedField_ReturnsNull()
    {
        var builder = new AuditChangeBuilder()
            .Add("name", "商品名称", "螺丝", "螺丝")
            .Add("purchasePrice", "采购价", AuditSummary.Money(10m), AuditSummary.Money(10m));

        Assert.Equal(0, builder.Count);
        Assert.False(builder.Truncated);
        Assert.Null(builder.Build());
    }

    /// <summary>敏感字段（密码 / 令牌 / 密钥）一律不记录</summary>
    [Theory]
    [InlineData("password")]
    [InlineData("passwordHash")]
    [InlineData("newPassword")]
    [InlineData("accessToken")]
    [InlineData("clientSecret")]
    [InlineData("apiKey")]
    public void AuditChangeBuilder_SensitiveField_IsDropped(string field)
    {
        var builder = new AuditChangeBuilder().Add(field, "敏感字段", "before", "after");

        Assert.Equal(0, builder.Count);
        Assert.Null(builder.Build());
    }

    /// <summary>角色 / 权限点等复数非密钥字段不被黑名单误伤</summary>
    [Fact]
    public void AuditChangeBuilder_PermissionKeys_IsKept()
    {
        var builder = new AuditChangeBuilder().Add("permissionKeys", "权限点", "-仓管", "+仓管 +财务");

        Assert.Equal(1, builder.Count);
        Assert.NotNull(builder.Build());
    }

    /// <summary>集合快照：前后差异整体作为一项记录</summary>
    [Fact]
    public void AuditChangeBuilder_CollectionSnapshot_KeepsBothSides()
    {
        var builder = new AuditChangeBuilder()
            .Add("roleIds", "角色", AuditSummary.Join(["仓管"]), AuditSummary.Join(["仓管", "财务"]));

        var json = builder.Build();

        Assert.NotNull(json);
        Assert.Contains("仓管、财务", json, StringComparison.Ordinal);
        Assert.Contains("\"field\":\"roleIds\"", json, StringComparison.Ordinal);
    }

    /// <summary>差异项可反序列化回结构化对象（字段名 camelCase）</summary>
    [Fact]
    public void AuditChangeBuilder_Build_ProducesReadableJson()
    {
        var builder = new AuditChangeBuilder()
            .Add("purchasePrice", "采购价", AuditSummary.Money(10m), AuditSummary.Money(8.8m));

        var items = JsonSerializer.Deserialize<List<AuditChangeItem>>(builder.Build()!, _changesReaderOptions);

        var item = Assert.Single(items!);
        Assert.Equal("purchasePrice", item.Field);
        Assert.Equal("采购价", item.Label);
        Assert.Equal("10.00", item.Before);
        Assert.Equal("8.80", item.After);
    }

    /// <summary>超长差异按尾部丢弃并置截断标记，结果不超上限</summary>
    [Fact]
    public void AuditChangeBuilder_TooManyChanges_TruncatesFromTail()
    {
        var builder = new AuditChangeBuilder();
        for (var i = 0; i < 400; i++)
        {
            builder.Add($"field{i}", $"字段{i}", new string('前', 200), new string('后', 200));
        }

        var json = builder.Build();

        Assert.NotNull(json);
        Assert.True(builder.Truncated);
        Assert.True(json.Length <= AuditLogFieldConstraints.ChangesMaxLength);
    }

    /// <summary>敏感字段判定：命中片段（password / token / secret / credential）或 Key 结尾</summary>
    [Fact]
    public void AuditChangeBuilder_IsSensitive_CoversBlacklist()
    {
        Assert.True(AuditChangeBuilder.IsSensitive("passwordHash"));
        Assert.True(AuditChangeBuilder.IsSensitive("accessToken"));
        Assert.True(AuditChangeBuilder.IsSensitive("apiKey"));
        Assert.True(AuditChangeBuilder.IsSensitive(string.Empty));
        Assert.False(AuditChangeBuilder.IsSensitive("permissionKeys"));
        Assert.False(AuditChangeBuilder.IsSensitive("purchasePrice"));
    }

    // ===== 4.5 查询用例 =====

    /// <summary>列表筛选（资源 + 动作 + 操作人 + 关键词）与时间倒序，且按分页返回</summary>
    [Fact]
    public async Task GetAuditLogs_FilterAndOrderAndPaging()
    {
        var (context, repository, operatorId) = await SeedAsync();

        var result = await new GetAuditLogsRequestHandler(repository).HandleAsync(new GetAuditLogsRequest
        {
            Resource = (int)AuditResource.Product,
            Page = 2,
            PageSize = 1,
        });

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal(AuditResource.Product, (AuditResource)item.Resource);

        var byAction = await new GetAuditLogsRequestHandler(repository).HandleAsync(new GetAuditLogsRequest
        {
            Action = (int)AuditAction.StatusChange,
        });
        Assert.Single(byAction.Items);
        Assert.Equal(AuditAction.StatusChange, (AuditAction)Assert.Single(byAction.Items).Action);

        var byUser = await new GetAuditLogsRequestHandler(repository).HandleAsync(new GetAuditLogsRequest
        {
            UserId = operatorId,
        });
        Assert.Equal(2, byUser.Total);

        var byKeyword = await new GetAuditLogsRequestHandler(repository).HandleAsync(new GetAuditLogsRequest
        {
            Keyword = "RC2026",
        });
        var keywordItem = Assert.Single(byKeyword.Items);
        Assert.Equal("RC202600000001", keywordItem.ResourceNo);
        Assert.Equal("财务小张", keywordItem.DisplayName);

        await context.DisposeAsync();
    }

    /// <summary>时间范围为闭区间，前后边界都包含</summary>
    [Fact]
    public async Task GetAuditLogs_TimeRange_IsInclusive()
    {
        var (context, repository, _) = await SeedAsync();

        var result = await new GetAuditLogsRequestHandler(repository).HandleAsync(new GetAuditLogsRequest
        {
            Start = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero),
        });

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, x => Assert.Equal("PO202600000002", x.ResourceNo));

        await context.DisposeAsync();
    }

    /// <summary>列表查询不加载差异列：实体投影排除 Changes，列表页不被大字段拖慢</summary>
    [Fact]
    public async Task GetAuditLogs_DoesNotLoadChanges()
    {
        var (context, repository, _) = await SeedAsync();

        var (pagedEntities, _) = await repository.GetPagedAsync(
            null, AuditResource.Product, null, null, null, null, 1, 20, default);

        Assert.All(pagedEntities, x => Assert.Null(x.Changes));
        var detail = await repository.GetByIdAsync(pagedEntities[0].Id);
        Assert.NotNull(detail?.Changes);

        await context.DisposeAsync();
    }

    /// <summary>详情：差异 JSON 反序列化为结构化数组</summary>
    [Fact]
    public async Task GetAuditLogById_DeserializesChanges()
    {
        var (context, repository, _) = await SeedAsync();
        var target = await repository.GetByIdAsync(SeededLogIds.ProductUpdate);

        var detail = await new GetAuditLogByIdRequestHandler(repository, NullLogger<GetAuditLogByIdRequestHandler>.Instance)
            .HandleAsync(new GetAuditLogByIdRequest { Id = target!.Id });

        Assert.Equal(new Guid(detail.Id), SeededLogIds.ProductUpdate);
        Assert.Equal(AuditResource.Product, (AuditResource)detail.Resource);
        Assert.Equal(AuditAction.Update, (AuditAction)detail.Action);
        Assert.Contains("编辑商品", detail.Summary, StringComparison.Ordinal);
        var change = Assert.Single(detail.Changes);
        Assert.Equal("采购价", change.Label);
        Assert.Equal("10.00", change.Before);
        Assert.Equal("8.80", change.After);

        await context.DisposeAsync();
    }

    /// <summary>详情：无差异时返回空数组（删除 / 作废类动作不展示空表格报错）</summary>
    [Fact]
    public async Task GetAuditLogById_NoChanges_ReturnsEmptyArray()
    {
        var (context, repository, _) = await SeedAsync();

        var detail = await new GetAuditLogByIdRequestHandler(repository, NullLogger<GetAuditLogByIdRequestHandler>.Instance)
            .HandleAsync(new GetAuditLogByIdRequest { Id = SeededLogIds.SettlementVoid });

        Assert.Empty(detail.Changes);
        Assert.NotNull(detail.UserId);

        await context.DisposeAsync();
    }

    /// <summary>详情：坏 JSON 兜底为空数组，不打断查询</summary>
    [Fact]
    public async Task GetAuditLogById_BrokenChangesJson_FallsBackToEmpty()
    {
        var (context, repository, _) = await SeedAsync();
        var broken = new AuditLog
        {
            Id = Guid.NewGuid(),
            Resource = AuditResource.Cost,
            Action = AuditAction.Recalculate,
            Summary = "成本重算",
            Changes = "{not-a-json",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.AuditLogs.Add(broken);
        await context.SaveChangesAsync();

        var detail = await new GetAuditLogByIdRequestHandler(repository, NullLogger<GetAuditLogByIdRequestHandler>.Instance)
            .HandleAsync(new GetAuditLogByIdRequest { Id = broken.Id });

        Assert.Empty(detail.Changes);

        await context.DisposeAsync();
    }

    /// <summary>详情：日志不存在抛 40400</summary>
    [Fact]
    public async Task GetAuditLogById_NotExists_ThrowsNotFound()
    {
        var context = TestSupport.CreateDbContext();
        var repository = new AuditLogRepository(context);

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => new GetAuditLogByIdRequestHandler(repository, NullLogger<GetAuditLogByIdRequestHandler>.Instance)
                .HandleAsync(new GetAuditLogByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, error.Code);

        await context.DisposeAsync();
    }

    // ===== 4.7 字段约束一致性 =====

    /// <summary>查询入参校验：关键词超长 / 非法枚举 / 时间倒挂 / 分页越界均按 40000 拒绝</summary>
    [Theory]
    [InlineData(nameof(GetAuditLogsRequest.Page), 0)]
    [InlineData(nameof(GetAuditLogsRequest.PageSize), 0)]
    [InlineData(nameof(GetAuditLogsRequest.PageSize), 101)]
    [InlineData(nameof(GetAuditLogsRequest.Resource), 99)]
    [InlineData(nameof(GetAuditLogsRequest.Action), 99)]
    public async Task GetAuditLogs_InvalidRange_IsRejected(string property, int value)
    {
        var request = new GetAuditLogsRequest();
        typeof(GetAuditLogsRequest).GetProperty(property)!.SetValue(request, value);

        var errors = await new GetAuditLogsRequestValidator().ValidateAsync(request);

        Assert.False(errors.IsValid);
    }

    /// <summary>关键词上限对齐 ResourceNo 列长：50 通过、51 拒绝</summary>
    [Fact]
    public async Task GetAuditLogs_KeywordLength_AlignsWithColumn()
    {
        var ok = await new GetAuditLogsRequestValidator().ValidateAsync(
            new GetAuditLogsRequest { Keyword = new string('A', AuditLogFieldConstraints.KeywordMaxLength) });
        var tooLong = await new GetAuditLogsRequestValidator().ValidateAsync(
            new GetAuditLogsRequest { Keyword = new string('A', AuditLogFieldConstraints.KeywordMaxLength + 1) });

        Assert.True(ok.IsValid);
        Assert.False(tooLong.IsValid);
    }

    /// <summary>开始时间不能晚于结束时间</summary>
    [Fact]
    public async Task GetAuditLogs_StartAfterEnd_IsRejected()
    {
        var errors = await new GetAuditLogsRequestValidator().ValidateAsync(new GetAuditLogsRequest
        {
            Start = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });

        Assert.False(errors.IsValid);
    }

    /// <summary>写入器：摘要超列长截断（差异超长时尾部追加截断标注）</summary>
    [Fact]
    public async Task AuditLogger_TruncatesSummaryAndMarksTruncation()
    {
        var context = TestSupport.CreateDbContext();
        var repository = new AuditLogRepository(context);
        var user = new StubCurrentUser(Guid.NewGuid(), "zhang", "张三");
        var logger = new AuditLogger(repository, user);

        await logger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.User,
            Action = AuditAction.Create,
            ResourceNo = "username-too-long-to-fit",
            Summary = new string('摘', AuditLogFieldConstraints.SummaryMaxLength + 20),
            UtcNow = DateTimeOffset.UtcNow,
        }, default);
        await logger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Role,
            Action = AuditAction.Update,
            Summary = "编辑角色 仓管",
            Changes = "[{\"field\":\"permissionKeys\",\"label\":\"权限点\",\"after\":\"+a\"}]",
            ChangesTruncated = true,
            UtcNow = DateTimeOffset.UtcNow,
        }, default);

        var longSummary = await repository.GetByIdAsync(
            context.AuditLogs.Single(x => x.Resource == AuditResource.User).Id);
        Assert.Equal(AuditLogFieldConstraints.SummaryMaxLength, longSummary!.Summary.Length);

        var marked = context.AuditLogs.Single(x => x.Resource == AuditResource.Role);
        Assert.EndsWith("（变更内容过长已截断）", marked.Summary, StringComparison.Ordinal);
        Assert.Equal("zhang", marked.Username);
        Assert.Equal("张三", marked.DisplayName);
        Assert.Equal(user.UserId, marked.UserId);

        await context.DisposeAsync();
    }

    /// <summary>写入器：无登录上下文时操作人为空（系统动作留空而非写假人）</summary>
    [Fact]
    public async Task AuditLogger_WithoutHttpContext_LeavesOperatorEmpty()
    {
        var context = TestSupport.CreateDbContext();
        var repository = new AuditLogRepository(context);

        await new AuditLogger(repository, new AnonymousCurrentUser()).RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Cost,
            Action = AuditAction.Recalculate,
            Summary = "成本重算",
            UtcNow = DateTimeOffset.UtcNow,
        }, default);

        var log = context.AuditLogs.Single();
        Assert.Null(log.UserId);
        Assert.Null(log.Username);
        Assert.Null(log.DisplayName);

        await context.DisposeAsync();
    }

    // ===== 测试数据 =====

    private static class SeededLogIds
    {
        public static readonly Guid ProductUpdate = Guid.NewGuid();
        public static readonly Guid ProductStatus = Guid.NewGuid();
        public static readonly Guid SettlementVoid = Guid.NewGuid();
    }

    private static async Task<(AppDbContext Context, AuditLogRepository Repository, Guid OperatorId)> SeedAsync()
    {
        var context = TestSupport.CreateDbContext();
        var repository = new AuditLogRepository(context);
        var operatorId = Guid.NewGuid();
        var secondOperatorId = Guid.NewGuid();

        context.AuditLogs.AddRange(
            NewLog(SeededLogIds.ProductUpdate, operatorId, AuditResource.Product, AuditAction.Update,
                "PO202600000002", "编辑商品 SKU-001 螺丝",
                "[{\"field\":\"purchasePrice\",\"label\":\"采购价\",\"before\":\"10.00\",\"after\":\"8.80\"}]",
                new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero)),
            NewLog(SeededLogIds.ProductStatus, operatorId, AuditResource.Product, AuditAction.StatusChange,
                "PO202600000002", "停用商品 SKU-001 螺丝", null,
                new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero)),
            NewLog(SeededLogIds.SettlementVoid, secondOperatorId, AuditResource.Settlement, AuditAction.Void,
                "RC202600000001", "作废收款单 RC202600000001", null,
                new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero), "caiwu", "财务小张"));
        await context.SaveChangesAsync();

        return (context, repository, operatorId);
    }

    private static AuditLog NewLog(
        Guid id,
        Guid userId,
        AuditResource resource,
        AuditAction action,
        string? resourceNo,
        string summary,
        string? changes,
        DateTimeOffset createdAt,
        string username = "operator",
        string? operatorDisplayName = null)
        => new()
        {
            Id = id,
            UserId = userId,
            Username = username,
            DisplayName = operatorDisplayName,
            Resource = resource,
            Action = action,
            ResourceId = Guid.NewGuid(),
            ResourceNo = resourceNo,
            Summary = summary,
            Changes = changes,
            CreatedAt = createdAt,
        };
}

/// <summary>无登录上下文桩（系统动作）：操作人三列均为空</summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public string Id => string.Empty;

    public string Username => string.Empty;

    public string DisplayName => string.Empty;
}
