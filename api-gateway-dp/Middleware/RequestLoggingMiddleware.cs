using System.Diagnostics;
using Serilog;

namespace api_gateway_dp.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path;
        var method = context.Request.Method;

        Log.Debug("Request started: {Method} {Path}", method, path);

        await _next(context);

        sw.Stop();
        Log.Information("Request completed: {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
            method, path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}
