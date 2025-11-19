using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using TFM.Broker.Interfaces;
using TFM.Broker.Services;
var builder = WebApplication.CreateBuilder(args);
// ====================================
// KESTREL CONFIGURATION (mTLS)
// ====================================
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = builder.Configuration["Certificates:BrokerCertPath"]!;
    var certPassword = builder.Configuration["Certificates:BrokerCertPassword"]!;
    var caCertPath = builder.Configuration["Certificates:CaCertPath"]!;
    // Puerto gRPC con mTLS
    options.ListenAnyIP(50051, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = new X509Certificate2(certPath, certPassword);
            httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            httpsOptions.ClientCertificateValidation = (cert, chain, errors) =>
            {
                var caCert = new X509Certificate2(caCertPath);
                var chain2 = new X509Chain();
                chain2.ChainPolicy.ExtraStore.Add(caCert);
                chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                bool isValid = chain2.Build(cert!);
                if (!isValid) return false;
                    var root = chain2.ChainElements[^1].Certificate;
                    return root.Thumbprint == caCert.Thumbprint;
                };
            });
        });
    // Puerto HTTP para Caddy
    options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http1);
    options.Limits.MaxRequestBodySize = 16 * 1024 * 1024; // 16 MB global
});
// ====================================
// SERVICES
// ====================================
builder.Services.AddGrpc();
// Single-tenant implementations
builder.Services.AddSingleton<ITunnelManager, SingleTunnelManager>();
builder.Services.AddSingleton<IRoutingService, FileRoutingService>();
var app = builder.Build();

// ====================================
// ENDPOINTS
// ====================================
app.MapGrpcService<TunnelGrpcService>();

// HTTP endpoint para Caddy
app.MapPost("/proxy", async (
    HttpContext context,
    ITunnelManager tunnelManager,
    IRoutingService routingService) =>
    {
        var domain = context.Request.Headers["X-Forwarded-Host"].ToString();
        if (string.IsNullOrEmpty(domain))
        {
            domain = context.Request.Host.Host;
        }

        if (!IsValidDomain(domain))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{\"error\": \"Invalid domain format\"}");
            return;
        }
        
        // Buscar ruta
        var route = await routingService.GetRouteForDomainAsync(domain);
        if (route == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"{{\"error\": \"Domain not configured: {domain}\"}}");
            return;
        }
        // Verificar que el agente está conectado
        if (!tunnelManager.IsAgentConnected(route.AgentId))
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync($"{{\"error\": \"Agent offline: {route.AgentId}\"}}");
            return;
        }
        // Convertir a gRPC HttpRequest
        var grpcRequest = new TFM.Contracts.HttpRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Method = context.Request.Method,
            Path = context.Request.Path + context.Request.QueryString,
            Host = domain,
            ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };
        foreach (var header in context.Request.Headers)
        {
            grpcRequest.Headers[header.Key] = header.Value.ToString();
        }
        if (context.Request.Body != null)
        {
            const long maxBodySize = 16 * 1024 * 1024; // 16 MB --> evitar DoS por memoria

            if (context.Request.ContentLength > maxBodySize)
            {
                context.Response.StatusCode = 413; // Payload Too Large
                await context.Response.WriteAsync("{\"error\": \"Request body too large\"}");
                return;
            }

            using var ms = new MemoryStream();
            using var limitedStream = new StreamReader(
                context.Request.Body,
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: false);

            var buffer = new byte[8192];
            int bytesRead;
            long totalBytes = 0;

            while ((bytesRead = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > maxBodySize)
                {
                    context.Response.StatusCode = 413;
                    await context.Response.WriteAsync("{\"error\": \"Request body too large\"}");
                    return;
                }
                await ms.WriteAsync(buffer, 0, bytesRead);
            }

            grpcRequest.Body = ByteString.CopyFrom(ms.ToArray());
        }
        try
        {
            // Enviar al agente
            var response = await tunnelManager.SendRequestToAgentAsync(
            route.AgentId,
            grpcRequest,
            TimeSpan.FromSeconds(30));
            // Devolver respuesta
            context.Response.StatusCode = response.StatusCode;
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value;
            }
            await context.Response.Body.WriteAsync(response.Body.ToByteArray());
        }
        catch (TimeoutException)
        {
            context.Response.StatusCode = 504;
            await context.Response.WriteAsync("{\"error\": \"Gateway timeout\"}");
        }
        catch (Exception ex)
        {
            // Loguear el error completo internamente
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error procesando petición para {Domain}", domain);

            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("{\"error\": \"Internal server error\"}");
        }
    });

app.MapGet("/health", () => "Broker OK");

// Middleware de autenticación para endpoints admin
var adminApiKey = builder.Configuration["Security:AdminApiKey"]
    ?? throw new InvalidOperationException("AdminApiKey no configurada");

app.MapPost("/admin/reload-routes", async (HttpContext context, IRoutingService routingService) =>
{
    // Validar API Key
    if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) || apiKey != adminApiKey)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("{\"error\": \"Unauthorized\"}");
        return Results.Unauthorized();
        
    }

    await routingService.ReloadRoutesAsync();
    return Results.Ok(new { message = "Routes reloaded" });
});

bool IsValidDomain(string domain)
{
    if (string.IsNullOrWhiteSpace(domain) || domain.Length > 253)
        return false;

    // RFC 1123 - solo letras, números, puntos y guiones
    return System.Text.RegularExpressions.Regex.IsMatch(
        domain,
        @"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)*[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    );
}

app.Run();