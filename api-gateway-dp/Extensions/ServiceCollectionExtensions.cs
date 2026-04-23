using api_gateway_dp.Infrastructure.Consul;
using api_gateway_dp.Proxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace api_gateway_dp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYarpReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IForwarderHttpClientFactory, PollyForwarderHttpClientFactory>();

        var consulHost = configuration["Consul:Host"];
        var discoveryEnabled = configuration.GetValue($"{ConsulDiscoveryOptions.SectionName}:Enabled", true);

        if (!string.IsNullOrWhiteSpace(consulHost) && discoveryEnabled)
        {
            services.Configure<ConsulDiscoveryOptions>(configuration.GetSection(ConsulDiscoveryOptions.SectionName));
            services.AddSingleton<InMemoryConfigProvider>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var routes = ReverseProxyConfigReader.LoadRoutes(cfg);
                var clusters = ReverseProxyConfigReader.LoadClusters(cfg);
                return new InMemoryConfigProvider(routes, clusters);
            });
            services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<InMemoryConfigProvider>());
            services.AddReverseProxy();
            services.AddHostedService<ConsulProxyRefresherHostedService>();
        }
        else
        {
            services.AddReverseProxy()
                .LoadFromConfig(configuration.GetSection("ReverseProxy"));
        }

        return services;
    }
}
