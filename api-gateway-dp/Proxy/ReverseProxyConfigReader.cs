using Microsoft.Extensions.Configuration;
using YarpClusterConfig = Yarp.ReverseProxy.Configuration.ClusterConfig;
using YarpDestinationConfig = Yarp.ReverseProxy.Configuration.DestinationConfig;
using YarpRouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;
using YarpRouteMatch = Yarp.ReverseProxy.Configuration.RouteMatch;

namespace api_gateway_dp.Proxy;

internal static class ReverseProxyConfigReader
{
    public static IReadOnlyList<YarpRouteConfig> LoadRoutes(IConfiguration configuration)
    {
        var list = new List<YarpRouteConfig>();
        var section = configuration.GetSection("ReverseProxy:Routes");
        foreach (var child in section.GetChildren())
        {
            var path = child.GetSection("Match:Path").Value;
            var clusterId = child["ClusterId"] ?? string.Empty;
            if (string.IsNullOrEmpty(clusterId) || string.IsNullOrEmpty(path))
            {
                continue;
            }

            list.Add(new YarpRouteConfig
            {
                RouteId = child.Key ?? string.Empty,
                ClusterId = clusterId,
                Match = new YarpRouteMatch { Path = path }
            });
        }

        return list;
    }

    public static IReadOnlyList<YarpClusterConfig> LoadClusters(IConfiguration configuration)
    {
        var list = new List<YarpClusterConfig>();
        var section = configuration.GetSection("ReverseProxy:Clusters");
        foreach (var clusterChild in section.GetChildren())
        {
            var dests = new Dictionary<string, YarpDestinationConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in clusterChild.GetSection("Destinations").GetChildren())
            {
                var address = d["Address"];
                if (!string.IsNullOrEmpty(address))
                {
                    dests[d.Key] = new YarpDestinationConfig { Address = address };
                }
            }

            list.Add(new YarpClusterConfig
            {
                ClusterId = clusterChild.Key,
                Destinations = dests
            });
        }

        return list;
    }

    public static YarpClusterConfig CloneCluster(YarpClusterConfig source)
    {
        var dests = new Dictionary<string, YarpDestinationConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source.Destinations ?? new Dictionary<string, YarpDestinationConfig>())
        {
            dests[kv.Key] = new YarpDestinationConfig { Address = kv.Value.Address ?? string.Empty };
        }

        return new YarpClusterConfig
        {
            ClusterId = source.ClusterId,
            Destinations = dests,
            LoadBalancingPolicy = source.LoadBalancingPolicy,
            HealthCheck = source.HealthCheck,
            HttpClient = source.HttpClient,
            HttpRequest = source.HttpRequest,
            Metadata = source.Metadata,
            SessionAffinity = source.SessionAffinity
        };
    }
}
