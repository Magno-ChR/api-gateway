using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace api_gateway_dp.Extensions;

public static class SerilogExtensions
{
    public static WebApplicationBuilder AddGatewaySerilog(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["Telemetry:ServiceName"] ?? "api-gateway";

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            var lokiUri = context.Configuration["Loki:Uri"];
            if (!string.IsNullOrWhiteSpace(lokiUri) &&
                Uri.TryCreate(lokiUri.Trim(), UriKind.Absolute, out var loki) &&
                (loki.Scheme == Uri.UriSchemeHttp || loki.Scheme == Uri.UriSchemeHttps))
            {
                loggerConfiguration.WriteTo.GrafanaLoki(
                    lokiUri.Trim(),
                    [new LokiLabel { Key = "service_name", Value = serviceName }]);
            }
        });

        return builder;
    }
}
