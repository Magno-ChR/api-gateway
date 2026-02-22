using api_gateway_dp.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogConfiguration();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddYarpReverseProxy(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandling();
app.UseSerilogRequestLogging();
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
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
