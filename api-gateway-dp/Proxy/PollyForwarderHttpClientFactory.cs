using System.Net;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Yarp.ReverseProxy.Forwarder;

namespace api_gateway_dp.Proxy;

/// <summary>
/// Fábrica de cliente HTTP para YARP que aplica políticas Polly (Circuit Breaker + Retry).
/// </summary>
public class PollyForwarderHttpClientFactory : IForwarderHttpClientFactory
{
    private readonly ILogger<PollyForwarderHttpClientFactory> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;

    public PollyForwarderHttpClientFactory(ILogger<PollyForwarderHttpClientFactory> logger)
    {
        _logger = logger;
        _policy = CreatePolicy();
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None
        };

        var policyHandler = new PolicyHttpMessageHandler(_policy)
        {
            InnerHandler = handler
        };

        return new HttpMessageInvoker(policyHandler);
    }

    private IAsyncPolicy<HttpResponseMessage> CreatePolicy()
    {
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                2,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (_, _, attempt, _) => _logger.LogWarning("Reintento {Attempt} por error transitorio", attempt));

        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable)
            .CircuitBreakerAsync(
                3,
                TimeSpan.FromSeconds(15),
                onBreak: (outcome, duration) =>
                    _logger.LogWarning("Circuit breaker abierto por {Duration}s. Razón: {Reason}",
                        duration.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()),
                onReset: () => _logger.LogInformation("Circuit breaker cerrado. Reintentando llamadas."));

        return Policy.WrapAsync(retry, circuitBreaker);
    }
}
