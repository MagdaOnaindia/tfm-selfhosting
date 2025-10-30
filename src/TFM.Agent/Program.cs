using TFM.Agent;
using TFM.Agent.Services;
var builder = Host.CreateApplicationBuilder(args);
// Configuración
builder.Configuration
.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
.AddEnvironmentVariables();
// Logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// Servicios
builder.Services.AddHttpClient("LocalProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "SelfHosting-Agent/1.0");
});
builder.Services.AddSingleton<ILocalProxy, LocalProxy>();
builder.Services.AddSingleton<ITunnelClient, TunnelClient>();
// Worker service
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Environment.Exit(1);
}
