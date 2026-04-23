using api_gateway_dp.Infrastructure.Consul;
using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using YarpClusterConfig = Yarp.ReverseProxy.Configuration.ClusterConfig;
using YarpRouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;

namespace api_gateway_dp.Proxy;

internal sealed class ConsulProxyRefresherHostedService(
    IConfiguration configuration,
    IConsulClient consulClient,
    Yarp.ReverseProxy.Configuration.InMemoryConfigProvider configProvider,
    IOptions<ConsulDiscoveryOptions> discoveryOptions,
    ILogger<ConsulProxyRefresherHostedService> logger) : BackgroundService
{
    private readonly ConsulDiscoveryOptions _discovery = discoveryOptions.Value;
    private readonly IReadOnlyList<YarpRouteConfig> _routes = ReverseProxyConfigReader.LoadRoutes(configuration);
    private readonly IReadOnlyList<YarpClusterConfig> _baseClusters = ReverseProxyConfigReader.LoadClusters(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _discovery.RefreshSeconds));
        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_routes.Count == 0 || _baseClusters.Count == 0)
        {
            return;
        }

        var clusters = new List<YarpClusterConfig>();
        foreach (var cluster in _baseClusters)
        {
            var clusterId = cluster.ClusterId ?? string.Empty;
            if (!_discovery.Clusters.TryGetValue(clusterId, out var consulServiceName) ||
                string.IsNullOrWhiteSpace(consulServiceName))
            {
                clusters.Add(ReverseProxyConfigReader.CloneCluster(cluster));
                continue;
            }

            try
            {
                var query = await consulClient.Health.Service(
                        consulServiceName,
                        string.Empty,
                        true,
                        new QueryOptions(),
                        cancellationToken)
                    .ConfigureAwait(false);
                var entries = query.Response;
                if (entries is null || entries.Length == 0)
                {
                    logger.LogDebug(
                        "Consul discovery: sin instancias passing para {ClusterId} ({Service}); fallback a configuración.",
                        clusterId,
                        consulServiceName);
                    clusters.Add(ReverseProxyConfigReader.CloneCluster(cluster));
                    continue;
                }

                var destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>(StringComparer.OrdinalIgnoreCase);
                var index = 0;
                foreach (var entry in entries)
                {
                    var host = string.IsNullOrWhiteSpace(entry.Service.Address)
                        ? entry.Node?.Address
                        : entry.Service.Address;
                    if (string.IsNullOrWhiteSpace(host))
                    {
                        continue;
                    }

                    var port = entry.Service.Port;
                    var address = $"http://{host}:{port}/";
                    var destKey = string.IsNullOrWhiteSpace(entry.Service.ID)
                        ? $"consul-{clusterId}-{index}"
                        : $"consul-{entry.Service.ID}";
                    destinations[destKey] = new Yarp.ReverseProxy.Configuration.DestinationConfig { Address = address };
                    index++;
                }

                if (destinations.Count == 0)
                {
                    clusters.Add(ReverseProxyConfigReader.CloneCluster(cluster));
                }
                else
                {
                    clusters.Add(new YarpClusterConfig
                    {
                        ClusterId = cluster.ClusterId!,
                        Destinations = destinations,
                        LoadBalancingPolicy = cluster.LoadBalancingPolicy,
                        HealthCheck = cluster.HealthCheck,
                        HttpClient = cluster.HttpClient,
                        HttpRequest = cluster.HttpRequest,
                        Metadata = cluster.Metadata,
                        SessionAffinity = cluster.SessionAffinity
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Consul discovery error para cluster {ClusterId}; fallback.", clusterId);
                clusters.Add(ReverseProxyConfigReader.CloneCluster(cluster));
            }
        }

        configProvider.Update(_routes, clusters);
    }
}
