using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace api_gateway_dp.Infrastructure.Consul;

public static class ConsulDependencyInjection
{
    /// <summary>Registra cliente Consul y registro del servicio. No hace nada si <c>Consul:Host</c> está vacío.</summary>
    public static IServiceCollection AddConsulServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration[$"{ConsulOptions.SectionName}:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            return services;
        }

        services.Configure<ConsulOptions>(configuration.GetSection(ConsulOptions.SectionName));
        services.AddSingleton<IConsulClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ConsulOptions>>().Value;
            return new ConsulClient(c => c.Address = new Uri(opts.Host));
        });
        services.AddHostedService<ConsulHostedService>();
        return services;
    }
}
