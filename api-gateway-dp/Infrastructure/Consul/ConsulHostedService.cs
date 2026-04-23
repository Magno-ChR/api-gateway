using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace api_gateway_dp.Infrastructure.Consul;

internal sealed class ConsulHostedService(
    IConsulClient consulClient,
    IOptions<ConsulOptions> options,
    ILogger<ConsulHostedService> logger) : IHostedService
{
    private readonly ConsulOptions _options = options.Value;
    private string? _registrationId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            logger.LogInformation("Consul: Host no configurado; se omite el registro de servicio.");
            return;
        }

        _registrationId = $"{_options.ServiceName}-{_options.ServiceAddress}-{_options.ServicePort}";

        var healthUrl =
            $"http://{_options.ServiceAddress}:{_options.ServicePort}{_options.HealthCheckEndpoint}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _options.ServiceName,
            Address = _options.ServiceAddress,
            Port = _options.ServicePort,
            Tags = _options.Tags,
            Check = new AgentServiceCheck
            {
                HTTP = healthUrl,
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        logger.LogInformation(
            "Consul: registrando servicio {ServiceName} en {Address}:{Port} (health: {HealthUrl})",
            _options.ServiceName,
            _options.ServiceAddress,
            _options.ServicePort,
            healthUrl);

        try
        {
            await consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
            await consulClient.Agent.ServiceRegister(registration, cancellationToken);
            logger.LogInformation("Consul: registro correcto con ID {RegistrationId}", _registrationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Consul: error al registrar el servicio");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registrationId is null)
        {
            return;
        }

        logger.LogInformation("Consul: dando de baja el servicio {RegistrationId}", _registrationId);

        try
        {
            await consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Consul: error al deregister el servicio");
        }
    }
}
