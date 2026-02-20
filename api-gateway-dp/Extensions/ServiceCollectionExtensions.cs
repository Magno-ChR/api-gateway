using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using api_gateway_dp.Proxy;
using Yarp.ReverseProxy.Forwarder;

namespace api_gateway_dp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYarpReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IForwarderHttpClientFactory, PollyForwarderHttpClientFactory>();
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"));

        return services;
    }
}
