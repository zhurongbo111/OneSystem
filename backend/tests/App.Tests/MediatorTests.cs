using App.Core.Abstractions;
using App.Core.Errors;
using App.Core.Mediation;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// 用例中介者（自研简化版 MediatR）测试
/// </summary>
public class MediatorTests
{
    [Fact]
    public async Task Send_已注册请求_应分发到对应处理器()
    {
        using var scope = CreateScope(services =>
            services.AddScoped<IRequestHandler<PingRequest, string>, PingRequestHandler>());

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PingRequest("hi"));

        Assert.Equal("pong:hi", result);
    }

    [Fact]
    public async Task Send_未注册处理器_应抛异常()
    {
        using var scope = CreateScope(_ => { });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PingRequest("hi")));

        Assert.Contains("未注册用例处理器", ex.Message);
    }

    [Fact]
    public async Task Send_处理器同步抛业务异常_应原样抛出()
    {
        using var scope = CreateScope(services =>
            services.AddScoped<IRequestHandler<BoomRequest, object>, BoomRequestHandler>());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => scope.ServiceProvider.GetRequiredService<IMediator>().Send(new BoomRequest()));

        Assert.Equal(ErrorCode.Forbidden, ex.Code);
    }

    private static IServiceScope CreateScope(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Mediator>();
        register(services);
        return services.BuildServiceProvider().CreateScope();
    }
}

internal sealed class PingRequest : IRequest<string>
{
    public PingRequest(string value)
    {
        Value = value;
    }

    public string Value { get; }
}

internal sealed class PingRequestHandler : IRequestHandler<PingRequest, string>
{
    public Task<string> HandleAsync(PingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult($"pong:{request.Value}");
}

internal sealed class BoomRequest : IRequest<object>
{
}

internal sealed class BoomRequestHandler : IRequestHandler<BoomRequest, object>
{
    public Task<object> HandleAsync(BoomRequest request, CancellationToken cancellationToken = default)
        => throw new BusinessException(ErrorCode.Forbidden, "拒绝访问");
}
