using Grpc.Core;
using TFM.Agent.Services;

namespace TFM.Agent;

/// <summary>
/// Worker service principal que mantiene el agente corriendo.
/// Implementa reconexión automática con backoff exponencial.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITunnelClient _tunnelClient;
    private readonly IConfiguration _configuration;
    public Worker(
    ILogger<Worker> logger,
    ITunnelClient tunnelClient,
    IConfiguration configuration)
    {
        _logger = logger;
        _tunnelClient = tunnelClient;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("? SelfHosting Agent Starting ?");

        var backoffSeconds = 1;
        const int maxBackoffSeconds = 30;
        var attemptCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attemptCount++;
                if (attemptCount > 1)
                {
                    _logger.LogInformation("?? Reconnection attempt #{Attempt}", attemptCount);
                }
                await _tunnelClient.ConnectAsync(stoppingToken);
                // Si llegamos aquí, la conexión se cerró normalmente
                _logger.LogWarning("?? Tunnel connection closed");
                // Reset backoff si la conexión duró suficiente tiempo
                backoffSeconds = 1;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("?? Agent shutting down (cancellation requested)");
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogInformation("?? Agent shutting down (RPC cancelled)");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "?? Connection failed");
                _logger.LogError(" Error type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError(" Message: {Message}", ex.Message);
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("?? Retrying in {Seconds} seconds...", backoffSeconds);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("?? Shutdown requested during backoff");
                        break;
                    }
                    // Exponential backoff
                    backoffSeconds = Math.Min(backoffSeconds * 2, maxBackoffSeconds);
                }
            }
        }
        _logger.LogInformation("? SelfHosting Agent Stopped ?");
    }
}