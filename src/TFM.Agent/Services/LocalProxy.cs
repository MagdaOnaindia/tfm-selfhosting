using Google.Protobuf;
using TFM.Contracts;
namespace TFM.Agent.Services;
/// <summary>
/// Proxy local que recibe requests del túnel y los reenvía a Traefik.
/// Preserva headers importantes para el routing correcto.
/// </summary>
public interface ILocalProxy
{
    Task<HttpResponse> HandleRequestAsync(HttpRequest request);
}
public class LocalProxy : ILocalProxy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalProxy> _logger;
    private static readonly string[] SkipHeaders = new[]
    {
        "Host",
        "Connection",
        "Keep-Alive",
        "Transfer-Encoding",
        "Upgrade",
        "Proxy-Connection",
        "Proxy-Authorization"
    };
    public LocalProxy(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LocalProxy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }
    public async Task<HttpResponse> HandleRequestAsync(HttpRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(" {Method} {Host}{Path}",
            request.Method, request.Host, request.Path);
            // URL de Traefik local
            var traefikUrl = _configuration["Agent:TraefikUrl"] ?? "http://localhost:80";
            // Construir URI completa
            var requestUri = new Uri(traefikUrl + request.Path);
            // Crear HttpRequestMessage
            var httpRequest = new HttpRequestMessage
            {
                Method = new HttpMethod(request.Method),
                RequestUri = requestUri
            };
            // ⚠️ CRÍTICO: Preservar Host header para Traefik routing
            httpRequest.Headers.Host = request.Host;
            // Copiar otros headers
            foreach (var header in request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ShouldSkipHeader(header.Key))
                    continue;
                try
                {
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add header {HeaderName}", header.Key);
                }
            }

            // Agregar headers de forwarding
            httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", request.ClientIp);
            httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            httpRequest.Headers.TryAddWithoutValidation("X-Real-IP", request.ClientIp);
            httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", request.Host);
            // Copiar body si existe
            if (request.Body != null && request.Body.Length > 0)
            {
                var bodyBytes1 = request.Body.ToByteArray();
                httpRequest.Content = new ByteArrayContent(bodyBytes1);
                if (request.Headers.TryGetValue("Content-Type", out var contentType))
                {
                    try
                    {
                        httpRequest.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                    }
                    catch
                    {
                        httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                    }
                }
                if (request.Headers.TryGetValue("Content-Length", out var contentLength))
                {
                    // Content-Length se maneja automáticamente
                }
            }

            // Ejecutar request
            var httpClient = _httpClientFactory.CreateClient("LocalProxy");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            _logger.LogDebug("→ Forwarding to Traefik: {Method} {Uri} (Host: {Host})",
            httpRequest.Method, httpRequest.RequestUri, httpRequest.Headers.Host);
            var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
            // Convertir respuesta
            var response = new HttpResponse
            {
                RequestId = request.RequestId,
                StatusCode = (int)httpResponse.StatusCode,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
            // Copiar response headers
            foreach (var header in httpResponse.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }
            // Content headers
            if (httpResponse.Content?.Headers != null)
            {
                foreach (var header in httpResponse.Content.Headers)
                {
                    response.Headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            // Copiar body
            var bodyBytes = await httpResponse.Content.ReadAsByteArrayAsync();
            response.Body = ByteString.CopyFrom(bodyBytes);
            _logger.LogInformation(" {Method} {Host}{Path} → {StatusCode} ({Size} bytes, {Time}ms)",
            request.Method, request.Host, request.Path,
            response.StatusCode, bodyBytes.Length, response.ProcessingTimeMs);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, " HTTP request error connecting to Traefik");
            _logger.LogError(" Make sure Traefik is running on {TraefikUrl}",
            _configuration["Agent:TraefikUrl"]);
            return CreateErrorResponse(request.RequestId, 502,
            "Bad Gateway - Cannot connect to local Traefik");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, " Request timeout");
            return CreateErrorResponse(request.RequestId, 504,
            "Gateway Timeout - Local service took too long to respond");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Unexpected error processing request");
            return CreateErrorResponse(request.RequestId, 500,
            "Internal Server Error");
        }
    }
    private bool ShouldSkipHeader(string headerName)
    {
        return SkipHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }
    private HttpResponse CreateErrorResponse(string requestId, int statusCode, string message)
    {
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = message,
            statusCode,
            timestamp = DateTime.UtcNow,
            source = "agent-proxy"
        });
        return new HttpResponse
        {
            RequestId = requestId,
            StatusCode = statusCode,
            Body = ByteString.CopyFromUtf8(errorJson),
            Headers =
            {
                { "Content-Type", "application/json; charset=utf-8" },
                { "X-Error-Source", "SelfHosting-Agent-Proxy" }
            }
        };
    }
}