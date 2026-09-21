using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace App.Api.Swagger;

/// <summary>
/// Swagger 操作过滤器：按认证白名单语义为 operation 显式标注 Bearer security 要求——
/// 动作方法或其控制器类型标注 [AllowAnonymous] 的接口不加（UI 不显示锁图标），其余接口（含仅靠
/// FallbackPolicy 默认要求登录的）均标注。替代文档级 AddSecurityRequirement（其作用于全部 operation，
/// 无法区分匿名接口），使文档锁图标与真实认证语义一致。
/// </summary>
public class SwaggerSecurityOperationFilter : IOperationFilter
{
    /// <summary>
    /// 非匿名接口标注 Bearer 安全要求
    /// </summary>
    /// <param name="operation">待生成的 OpenAPI 操作</param>
    /// <param name="context">过滤器上下文（含动作方法信息）</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var attributes = method.GetCustomAttributesData()
            .Concat(method.DeclaringType?.GetCustomAttributesData() ?? Enumerable.Empty<CustomAttributeData>());

        var isAnonymous = attributes.Any(attr => attr.AttributeType == typeof(AllowAnonymousAttribute));
        if (isAnonymous)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
                },
                Array.Empty<string>()
            }
        });
    }
}
