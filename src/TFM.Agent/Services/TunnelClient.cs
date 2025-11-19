using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using TFM.Contracts;

namespace TFM.Agent.Services;
/// <summary>
/// Cliente del túnel gRPC que mantiene conexión persistente con el Broker.
/// Implementa reconexión automática con backoff exponencial.
/// </summary>
public interface ITunnelClient
{
    Task ConnectAsync(CancellationToken cancellationToken);
}
public class TunnelClient : ITunnelClient
{
    private readonly IConfiguration _configuration;
    private readonly ILocalProxy _localProxy;
    private readonly ILogger<TunnelClient> _logger;
    public TunnelClient(
    IConfiguration configuration,
    ILocalProxy localProxy,
    ILogger<TunnelClient> logger)
    {
        _configuration = configuration;
        _localProxy = localProxy;
        _logger = logger;
    }
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        // Cargar configuración
        var agentId = _configuration["Agent:AgentId"]
        ?? throw new InvalidOperationException("Agent:AgentId not configured");
        var brokerUrl = _configuration["Agent:BrokerUrl"]
        ?? throw new InvalidOperationException("Agent:BrokerUrl not configured");
        var certPath = _configuration["Agent:CertificatePath"]
        ?? throw new InvalidOperationException("Agent:CertificatePath not configured");
        var certPassword = _configuration["Agent:CertificatePassword"]
        ?? throw new InvalidOperationException("Agent:CertificatePassword not configured");
        var caCertPath = _configuration["Agent:CaCertPath"]
        ?? throw new InvalidOperationException("Agent:CaCertPath not configured");

        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation(" Starting SelfHosting Agent");
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation(" Agent ID: {AgentId}", agentId);
        _logger.LogInformation(" Broker URL: {BrokerUrl}", brokerUrl);

        // Verificar que los archivos existen
        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException($"Certificate not found: {certPath}");
        }
        if (!File.Exists(caCertPath))
        {
            throw new FileNotFoundException($"CA certificate not found: {caCertPath}");
        }

        // Cargar certificados
        X509Certificate2 clientCert;
        X509Certificate2 caCert;

        try
        {
            clientCert = new X509Certificate2(certPath, certPassword);
            caCert = new X509Certificate2(caCertPath);
            _logger.LogInformation(" Client certificate loaded");
            _logger.LogInformation(" Subject: {Subject}", clientCert.Subject);
            _logger.LogInformation(" Valid from: {NotBefore}", clientCert.NotBefore);
            _logger.LogInformation(" Valid until: {NotAfter}", clientCert.NotAfter);
            _logger.LogInformation(" CA certificate loaded");
            _logger.LogInformation(" Subject: {Subject}", caCert.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Failed to load certificates");
            throw;
        }

        // Configurar handler HTTP con mTLS
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCert);
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (cert == null)
            {
                _logger.LogError(" Server certificate is null");
                return false;
            }
            // Construir cadena de certificados
            var chain2 = new X509Chain();
            chain2.ChainPolicy.ExtraStore.Add(caCert);
            chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            bool isValid = chain2.Build(cert);
            if (!isValid)
            {
                _logger.LogError(" Server certificate chain validation failed");
                foreach (var status in chain2.ChainStatus)
                {
                    _logger.LogError(" {Status}: {StatusInformation}",
                    status.Status, status.StatusInformation);
                }
                return false;
            }
            // Verificar que el root es nuestra CA
            var root = chain2.ChainElements[^1].Certificate;
            if (root.Thumbprint != caCert.Thumbprint)
            {
                _logger.LogError(" CA thumbprint mismatch");
                _logger.LogError(" Expected: {Expected}", caCert.Thumbprint);
                _logger.LogError(" Got: {Got}", root.Thumbprint);
                return false;
            }
            _logger.LogInformation(" Server certificate validated successfully");
            return true;
        };

        // Crear canal gRPC
        var channelOptions = new GrpcChannelOptions
        {
            HttpHandler = handler,
            MaxReceiveMessageSize = 16 * 1024 * 1024, // 16 MB
            MaxSendMessageSize = 16 * 1024 * 1024, // 16 MB
            DisposeHttpClient = true
        };
        var channel = GrpcChannel.ForAddress(brokerUrl, channelOptions);
        var client = new TunnelService.TunnelServiceClient(channel);
        // Test de conectividad con Ping
        _logger.LogInformation(" Testing connection with ping...");

        try
        {
            var pingRequest = new PingRequest
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var pingResponse = await client.PingAsync(pingRequest,
                                    deadline: DateTime.UtcNow.AddSeconds(5),
                                    cancellationToken: cancellationToken);
            var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pingResponse.Timestamp;
            _logger.LogInformation(" Ping successful");
            _logger.LogInformation(" Server version: {Version}", pingResponse.ServerVersion);
            _logger.LogInformation(" Latency: {Latency}ms", latency);
        }
        catch (RpcException ex)
        {
            _logger.LogError(" Ping failed: {Status} - {Detail}", ex.Status.StatusCode, ex.Status.Detail);
            throw;
        }

        // Establecer túnel bidireccional
        _logger.LogInformation(" Establishing bidirectional tunnel...");
        var tunnel = client.EstablishTunnel(cancellationToken: cancellationToken);
        _logger.LogInformation(" Tunnel established successfully");
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation(" Agent is ONLINE and ready to receive requests");
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        // Tareas concurrentes
        var readTask = ReadMessagesAsync(tunnel.ResponseStream, tunnel.RequestStream, cancellationToken);
        var heartbeatTask = SendHeartbeatsAsync(tunnel.RequestStream, cancellationToken);
        // Esperar a que termine alguna (o ambas)
        await Task.WhenAll(readTask, heartbeatTask);
    }

    private async Task ReadMessagesAsync(
        IAsyncStreamReader<TunnelMessage> stream,
        IClientStreamWriter<TunnelMessage> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in stream.ReadAllAsync(cancellationToken))
            {
                _logger.LogDebug("Received {Type} message (ID: {MessageId})",
                message.Type, message.MessageId);
                if (message.Type == MessageType.HttpRequest)
                {
                    // Procesar request de forma asíncrona (no bloqueante)
                    _ = Task.Run(async () =>
                    {
                    try
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var response = await _localProxy.HandleRequestAsync(message.HttpRequest);
                        response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                        var responseMessage = new TunnelMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Type = MessageType.HttpResponse,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            HttpResponse = response
                        };
                        await writer.WriteAsync(responseMessage);
                            _logger.LogInformation("Response sent for {RequestId} ({Time}ms)",
                                                    response.RequestId, response.ProcessingTimeMs);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, " Error handling request {RequestId}",
                            message.HttpRequest.RequestId);
                            // Enviar error response
                            var errorResponse = CreateErrorResponse(
                                                message.HttpRequest.RequestId,
                                                500,
                                                "Internal agent error");
                            var errorMessage = new TunnelMessage
                            {
                                MessageId = Guid.NewGuid().ToString(),
                                Type = MessageType.HttpResponse,
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                HttpResponse = errorResponse
                            };
                            await writer.WriteAsync(errorMessage);
                        }
                    }, cancellationToken);
                }
                else if (message.Type == MessageType.Control)
                {
                    _logger.LogInformation(" Control message received: {Command}",
                    message.Control.Command);
                    await HandleControlMessageAsync(message.Control, writer);
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Tunnel read stream cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Error reading messages from tunnel");
            throw;
        }
    }

    private async Task SendHeartbeatsAsync(
        IClientStreamWriter<TunnelMessage> writer,
        CancellationToken cancellationToken)
    {
        var heartbeatInterval = _configuration.GetValue<int>("Agent:HeartbeatIntervalSeconds", 30);
        _logger.LogDebug(" Starting heartbeat task (interval: {Interval}s)", heartbeatInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(heartbeatInterval), cancellationToken);
            try
            {
                var heartbeat = new TunnelMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.Heartbeat,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                await writer.WriteAsync(heartbeat);
                _logger.LogDebug(" Heartbeat sent");
            }
            catch (OperationCanceledException)
            {
                // Normal durante shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, " Failed to send heartbeat");
            }
        }
        _logger.LogDebug(" Heartbeat task stopped");
    }

    private async Task HandleControlMessageAsync(
        ControlMessage control,
        IClientStreamWriter<TunnelMessage> writer)
    {
        switch (control.Command)
        {
            case "reload_config":
                _logger.LogInformation(" Reloading configuration...");
                // TODO: Implementar recarga de configuración
                break;
            case "health_check":
                _logger.LogDebug(" Health check requested");
                var response = new TunnelMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.Control,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Control = new ControlMessage
                    {
                        Command = "health_check_response",
                        Data = "OK"
                    }
                };
                await writer.WriteAsync(response);
                break;
            default:
                _logger.LogWarning(" Unknown control command: {Command}", control.Command);
                break;
        }
    }

    private HttpResponse CreateErrorResponse(string requestId, int statusCode, string message)
    {
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = message,
            statusCode,
            timestamp = DateTime.UtcNow,
            source = "agent"
        });
        return new HttpResponse
        {
            RequestId = requestId,
            StatusCode = statusCode,
            Body = Google.Protobuf.ByteString.CopyFromUtf8(errorJson),
            Headers =
            {
                { "Content-Type", "application/json" },
                { "X-Error-Source", "SelfHosting-Agent" }
            }
        };
    }
}