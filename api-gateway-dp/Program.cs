using api_gateway_dp.Extensions;
using api_gateway_dp.Infrastructure.Consul;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddGatewaySerilog();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddYarpReverseProxy(builder.Configuration);
builder.Services.AddHealthChecks();

var serviceName = builder.Configuration["Telemetry:ServiceName"] ?? "api-gateway";
var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint) &&
    Uri.TryCreate(otlpEndpoint.Trim(), UriKind.Absolute, out var otlpUri))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(rb => rb.AddService(serviceName))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = otlpUri))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = otlpUri));
}

var app = builder.Build();

app.UseExceptionHandling();
app.UseSerilogRequestLogging();
app.UseRequestLogging();
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => true
});

app.MapReverseProxy();
app.MapControllers();

try
{
    Log.Information("Starting API Gateway");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
