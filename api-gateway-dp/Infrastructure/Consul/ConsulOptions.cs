namespace api_gateway_dp.Infrastructure.Consul;

public sealed class ConsulOptions
{
    public const string SectionName = "Consul";

    /// <summary>URL del agente Consul, p. ej. http://INFRA_HOST:8500</summary>
    public string Host { get; set; } = string.Empty;

    public string ServiceName { get; set; } = "api-gateway";

    /// <summary>IP o hostname desde el que Consul puede alcanzar el gateway (p. ej. IP del droplet).</summary>
    public string ServiceAddress { get; set; } = "localhost";

    public int ServicePort { get; set; } = 5000;

    public string[] Tags { get; set; } = ["dotnet", "api", "gateway", "metrics"];

    public string HealthCheckEndpoint { get; set; } = "/health/live";
}
