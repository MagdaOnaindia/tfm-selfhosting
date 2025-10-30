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
            using var ms = new MemoryStream();
            await context.Request.Body.CopyToAsync(ms);
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
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"{{\"error\": \"{ex.Message}\"}}");
        }
    });

app.MapGet("/health", () => "Broker OK");

// Endpoint para recargar configuración
app.MapPost("/admin/reload-routes", async (IRoutingService routingService) =>
{
    await routingService.ReloadRoutesAsync();
    return Results.Ok(new { message = "Routes reloaded" });
});

app.Run();