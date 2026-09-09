using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Auth.Login;
using App.Core.Features.Users;
using App.Core.Features.Users.GetCurrentUser;
using App.Core.Mediation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core;

/// <summary>
/// App.Core 服务注册扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 App.Core 层服务（TokenService、IMediator、RequestHandler、RequestValidator）。
    /// 仓储实现与 IUnitOfWork 实现见 App.Infrastructure。
    /// </summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<TokenService>();

        // 用例中介：Controller 只注入 IMediator，经 Send(Request) 分发到已注册的用例处理器
        services.AddScoped<IMediator, Mediator>();

        // 用例处理器：每 API 一个 RequestHandler，统一注册为 IRequestHandler<TRequest,TResponse> 接口映射
        services.AddScoped<IRequestHandler<LoginRequest, LoginResponse>, LoginRequestHandler>();
        services.AddScoped<IRequestHandler<GetCurrentUserRequest, UserDto>, GetCurrentUserRequestHandler>();

        // 格式校验器（FluentValidation）：校验规则集中在对应用例目录
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

        return services;
    }
}
