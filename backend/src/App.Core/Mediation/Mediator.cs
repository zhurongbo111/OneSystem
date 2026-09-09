using System.Reflection;
using System.Runtime.ExceptionServices;
using App.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core.Mediation;

/// <summary>
/// 默认用例中介者（自研简化版 MediatR）：按请求的运行时类型从 DI 解析已注册的
/// IRequestHandler{TRequest,TResponse} 并调用其 HandleAsync，目前只支持请求 / 响应分发。
/// 请求与处理器须在 AddCore 显式注册，注册期不做反射扫描（Send 时才按类型解析单个已注册实现）。
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// 初始化用例中介者
    /// </summary>
    /// <param name="services">用于解析处理器实例的服务提供器</param>
    public Mediator(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = _services.GetService(handlerType)
            ?? throw new InvalidOperationException($"未注册用例处理器：{handlerType}，请在 App.Core 的 AddCore 中显式注册。");

        var handleAsync = handlerType.GetMethod(nameof(IRequestHandler<object, object>.HandleAsync))!;

        object? result;
        try
        {
            result = handleAsync.Invoke(handler, new object[] { request, cancellationToken });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // 非 async 实现同步抛出的异常会被 Invoke 包装，此处还原后按原样抛出
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        return await (result is Task<TResponse> task
            ? task
            : throw new InvalidOperationException($"处理器 {handlerType} 的 HandleAsync 返回类型与响应类型不符。"));
    }
}
