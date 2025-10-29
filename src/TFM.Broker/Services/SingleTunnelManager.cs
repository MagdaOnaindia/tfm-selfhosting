// src/TFM.Broker/Services/SingleTunnelManager.cs
using System.Collections.Concurrent;
using System.Threading.Channels;
using Grpc.Core;
using TFM.Broker.Interfaces;
using TFM.Contracts;
using HttpRequest = TFM.Contracts.HttpRequest;
using HttpResponse = TFM.Contracts.HttpResponse;

namespace TFM.Broker.Services;

public class SingleTunnelManager : ITunnelManager
{
    private readonly ILogger<SingleTunnelManager> _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<HttpResponse>> _pendingRequests = new();

    // Variables específicas para single-tenant
    private string? _currentAgentId;
    private IServerStreamWriter<TunnelMessage>? _agentWriter;
    private Channel<TunnelMessage>? _messageChannel;
    private CancellationToken _agentCancellationToken;

    public SingleTunnelManager(ILogger<SingleTunnelManager> logger)
    {
        _logger = logger;
    }

    public Task RegisterAgentAsync(string agentId, IServerStreamWriter<TunnelMessage> writer, CancellationToken cancellationToken)
    {
        if (_currentAgentId != null)
        {
            _logger.LogWarning("El Agente {NewAgentId} intentó conectar pero ya hay un agente conectado: {CurrentAgentId}", agentId, _currentAgentId);
            throw new InvalidOperationException("Solo se permite un agente en modo single-tenant.");
        }

        _currentAgentId = agentId;
        _agentWriter = writer;
        _messageChannel = Channel.CreateUnbounded<TunnelMessage>();
        _agentCancellationToken = cancellationToken;

        _logger.LogInformation("Agente {AgentId} conectado.", agentId);

        // Iniciar una tarea de fondo para enviar mensajes al agente
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    await _agentWriter.WriteAsync(message);
                }
            }
            catch (OperationCanceledException) { _logger.LogInformation("Tarea de escritura al agente cancelada."); }
            catch (Exception ex) { _logger.LogError(ex, "Error en la tarea de escritura al agente."); }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task UnregisterAgentAsync(string agentId)
    {
        if (_currentAgentId != agentId)
        {
            _logger.LogWarning("Intento de desregistrar un agente desconocido: {AgentId}", agentId);
            return Task.CompletedTask;
        }

        _messageChannel?.Writer.Complete();
        _currentAgentId = null;
        _agentWriter = null;
        _messageChannel = null;
        _logger.LogWarning("Agente {AgentId} desconectado.", agentId);
        return Task.CompletedTask;
    }

    public async Task<HttpResponse> SendRequestToAgentAsync(string agentId, HttpRequest request, TimeSpan timeout)
    {
        if (_currentAgentId != agentId || _messageChannel == null)
            throw new InvalidOperationException($"Agente {agentId} no está conectado.");

        var tcs = new TaskCompletionSource<HttpResponse>();
        if (!_pendingRequests.TryAdd(request.RequestId, tcs))
            throw new InvalidOperationException("Ya existe una petición con el mismo ID.");

        try
        {
            var message = new TunnelMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.HttpRequest,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                HttpRequest = request
            };
            await _messageChannel.Writer.WriteAsync(message);
            _logger.LogInformation("Petición {RequestId} enviada al agente.", request.RequestId);

            // Esperar la respuesta con timeout
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }
            else
            {
                throw new TimeoutException($"La petición {request.RequestId} ha expirado.");
            }
        }
        finally
        {
            _pendingRequests.TryRemove(request.RequestId, out _);
        }
    }

    public Task ReceiveResponseFromAgentAsync(string agentId, HttpResponse response)
    {
        if (_pendingRequests.TryRemove(response.RequestId, out var tcs))
        {
            tcs.SetResult(response);
            _logger.LogInformation("Respuesta para {RequestId} recibida del agente.", response.RequestId);
        }
        else
        {
            _logger.LogWarning("Se recibió una respuesta para una petición desconocida o expirada: {RequestId}", response.RequestId);
        }
        return Task.CompletedTask;
    }

    public bool IsAgentConnected(string agentId) => _currentAgentId == agentId && _agentWriter != null;

    public Task UpdateHeartbeatAsync(string agentId)
    {
        if (_currentAgentId == agentId)
            _logger.LogDebug("Heartbeat recibido de {AgentId}", agentId);

        return Task.CompletedTask;
    }
}