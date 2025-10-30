using Grpc.Core;
using TFM.Broker.Interfaces;
using TFM.Contracts;

public class TunnelGrpcService : TunnelService.TunnelServiceBase
{
    private readonly ITunnelManager _tunnelManager;
    private readonly ILogger<TunnelGrpcService> _logger;
    public TunnelGrpcService(
    ITunnelManager tunnelManager,
    ILogger<TunnelGrpcService> logger)
    {
        _tunnelManager = tunnelManager;
        _logger = logger;
    }
    public override async Task EstablishTunnel(
    IAsyncStreamReader<TunnelMessage> requestStream,
    IServerStreamWriter<TunnelMessage> responseStream,
ServerCallContext context)
    {
        // Extraer agentId del certificado cliente
        var agentId = context.AuthContext.PeerIdentity
        .FirstOrDefault(p => p.Name == "x509_common_name")?.Value;
        if (string.IsNullOrEmpty(agentId))
        {
            _logger.LogError("Client certificate missing CN");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid client certificate"));
        }
        _logger.LogInformation("￿ Tunnel request from: {AgentId}", agentId);
        try
        {
            // Registrar conexión
            await _tunnelManager.RegisterAgentAsync(agentId, responseStream, context.CancellationToken);
            // Leer mensajes del agente
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                _logger.LogDebug("← Received {Type} message from {AgentId}",
                message.Type, agentId);
                switch (message.Type)
                {
                    case MessageType.HttpResponse:
                        await _tunnelManager.ReceiveResponseFromAgentAsync(
                        agentId,
                        message.HttpResponse);
                        break;
                    case MessageType.Heartbeat:
                        await _tunnelManager.UpdateHeartbeatAsync(agentId);
                        break;
                    case MessageType.Control:
                        _logger.LogInformation("Control message from {AgentId}: {Command}",
                        agentId, message.Control.Command);
                        break;
                    default:
                        _logger.LogWarning("Unknown message type: {Type}", message.Type);
                        break;
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Tunnel cancelled for {AgentId}", agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tunnel error for {AgentId}", agentId);
        }
        finally
        {
            await _tunnelManager.UnregisterAgentAsync(agentId);
            _logger.LogInformation("￿ Tunnel closed for {AgentId}", agentId);
        }
    }
    public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PingResponse
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ServerVersion = "1.0.0-single"
        });
    }
}