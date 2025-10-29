using Grpc.Core;
using TFM.Contracts;
using HttpRequest = TFM.Contracts.HttpRequest;
using HttpResponse = TFM.Contracts.HttpResponse;

namespace TFM.Broker.Interfaces;

/// <summary>
/// Gestiona las conexiones de túneles con agentes.
/// En single-tenant: Gestiona UNA conexión.
/// En multi-tenant: Gestionaría MÚLTIPLES conexiones.
/// </summary>
public interface ITunnelManager
{
    Task RegisterAgentAsync(string agentId, IServerStreamWriter<TunnelMessage> writer, CancellationToken cancellationToken);
    Task UnregisterAgentAsync(string agentId);
    Task<HttpResponse> SendRequestToAgentAsync(string agentId, HttpRequest request, TimeSpan timeout);
    Task ReceiveResponseFromAgentAsync(string agentId, HttpResponse response);
    bool IsAgentConnected(string agentId);
    Task UpdateHeartbeatAsync(string agentId);
}