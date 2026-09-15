using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace App.Api.Observability;

/// <summary>
/// OpenTelemetry 服务注册（从 Program.cs 拆分）
/// </summary>
internal static class OpenTelemetryRegistration
{
    /// <summary>
    /// Tracing + Metrics 自动埋点（ASP.NET Core / EF Core / 运行时），OTLP 仅在配置 OTEL_EXPORTER_OTLP_ENDPOINT 时导出
    /// </summary>
    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        var otlpEnabled = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("app-api", "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();
                if (otlpEnabled)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation();
                if (otlpEnabled)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
