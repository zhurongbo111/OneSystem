using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.AccountingPeriods.ClosePeriod;
using App.Core.Features.AccountingPeriods.GetPeriods;
using App.Core.Features.AccountingPeriods.ReversePeriod;

namespace App.Tests;

/// <summary>
/// 会计期间用例测试（specs/033-erp-general-ledger tasks 6.3）：
/// 列表映射；结账 / 反结账成功且写审计、重复操作幂等（不重复写日志）、期间不存在 40400
/// </summary>
public class AccountingPeriodRequestHandlerTests
{
    [Fact]
    public async Task 查询期间_应按年月升序返回并映射状态()
    {
        var gl = GeneralLedgerStubs.Create();
        gl.Periods.Seed(2026, 10);
        gl.Periods.Seed(2026, 9, PeriodStatus.Closed);
        var handler = new GetPeriodsRequestHandler(gl.Periods);

        var result = await handler.HandleAsync(new GetPeriodsRequest { Year = 2026 });

        Assert.Equal(2, result.Count);
        Assert.Equal(9, result[0].Month);
        Assert.Equal((int)PeriodStatus.Closed, result[0].Status);
        Assert.Equal(10, result[1].Month);
        Assert.Equal((int)PeriodStatus.Open, result[1].Status);
    }

    [Fact]
    public async Task 结账_成功_应置已结账并写审计()
    {
        var gl = GeneralLedgerStubs.Create();
        var period = gl.Periods.Seed(2026, 9);
        var audit = new RecordingAuditLogger();
        var user = new StubCurrentUser(Guid.NewGuid());
        var handler = new ClosePeriodRequestHandler(gl.Periods, new RecordingUnitOfWork(), user, audit);

        var result = await handler.HandleAsync(new ClosePeriodRequest { Id = period.Id });

        Assert.Equal((int)PeriodStatus.Closed, result.Status);
        Assert.NotNull(result.ClosedAt);
        Assert.Equal(user.UserId.ToString(), result.ClosedBy);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.AccountingPeriod, entry.Resource);
        Assert.Equal(AuditAction.Close, entry.Action);
        Assert.Equal("2026-09", entry.ResourceNo);
    }

    [Fact]
    public async Task 结账_已结账_应幂等且不重复写审计()
    {
        var gl = GeneralLedgerStubs.Create();
        var period = gl.Periods.Seed(2026, 9, PeriodStatus.Closed);
        var audit = new RecordingAuditLogger();
        var handler = new ClosePeriodRequestHandler(gl.Periods, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new ClosePeriodRequest { Id = period.Id });

        Assert.Equal((int)PeriodStatus.Closed, result.Status);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task 结账_期间不存在_应报NotFound()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = new ClosePeriodRequestHandler(gl.Periods, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ClosePeriodRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 反结账_成功_应置未结账并清结账信息()
    {
        var gl = GeneralLedgerStubs.Create();
        var period = gl.Periods.Seed(2026, 9, PeriodStatus.Closed);
        period.ClosedAt = DateTimeOffset.UtcNow;
        period.ClosedBy = Guid.NewGuid();
        var audit = new RecordingAuditLogger();
        var handler = new ReversePeriodRequestHandler(gl.Periods, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new ReversePeriodRequest { Id = period.Id });

        Assert.Equal((int)PeriodStatus.Open, result.Status);
        Assert.Null(result.ClosedAt);
        Assert.Null(result.ClosedBy);
        Assert.Equal(AuditAction.Update, Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task 反结账_未结账_应幂等且不重复写审计()
    {
        var gl = GeneralLedgerStubs.Create();
        var period = gl.Periods.Seed(2026, 9);
        var audit = new RecordingAuditLogger();
        var handler = new ReversePeriodRequestHandler(gl.Periods, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), audit);

        var result = await handler.HandleAsync(new ReversePeriodRequest { Id = period.Id });

        Assert.Equal((int)PeriodStatus.Open, result.Status);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task 反结账_期间不存在_应报NotFound()
    {
        var gl = GeneralLedgerStubs.Create();
        var handler = new ReversePeriodRequestHandler(gl.Periods, new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ReversePeriodRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
