namespace api_gateway_dp.Infrastructure.Consul;

public sealed class ConsulDiscoveryOptions
{
    public const string SectionName = "ConsulDiscovery";

    public bool Enabled { get; set; } = true;

    public int RefreshSeconds { get; set; } = 30;

    /// <summary>ClusterId (YARP) → nombre del servicio en Consul.</summary>
    public Dictionary<string, string> Clusters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
