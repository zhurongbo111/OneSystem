using App.Core.Auth;
using App.Core.Handlers;
using App.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core;

/// <summary>
/// App.Core 服务注册扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 App.Core 层服务（Handler / TokenService / 账号服务）
    /// </summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<TokenService>();
        services.AddScoped<IUserAccountService, InMemoryUserAccountService>();
        services.AddScoped<IAuthHandler, AuthHandler>();
        services.AddScoped<IUserHandler, UserHandler>();
        return services;
    }
}
