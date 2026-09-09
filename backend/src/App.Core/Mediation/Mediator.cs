using System.Reflection;
using System.Runtime.ExceptionServices;
using App.Core.Abstractions;
using App.Core.Errors;
using FluentValidation;
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
        // 全局统一格式校验：按请求运行时类型解析已注册的 IValidator（未注册则跳过），失败抛 40000
        var validation = await ValidateRequestAsync(_services, request);
        if (!validation.IsValid)
        {
            throw new BusinessException(ErrorCode.Validation, validation.Errors.First().ErrorMessage);
        }

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

    /// <summary>
    /// 按请求运行时类型执行已注册的格式校验；未注册校验器时返回有效结果（视为校验通过）。
    /// </summary>
    /// <param name="request">用例请求（运行时类型即 TRequest）</param>
    /// <returns>格式校验结果；无校验器时为有效结果</returns>
    private static async Task<FluentValidation.Results.ValidationResult> ValidateRequestAsync(IServiceProvider services, object request)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(request.GetType());
        if (services.GetService(validatorType) is not IValidator validator)
        {
            return new FluentValidation.Results.ValidationResult();
        }

        // 非泛型 IValidator 无法直接绑定带 T 参数的 FluentValidation 扩展方法，经 NonGenericValidatorExtensions 运行时绑定
        return await NonGenericValidatorExtensions.ValidateAsync(validator, request);
    }
}
