using MudBlazor.Services;
using TFM.Dashboard.Components;
using TFM.Dashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();


builder.Services.AddDashboardServices();


var app = builder.Build();



if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();


app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Json(new
{
    status = "healthy",
    service = "dashboard",
    timestamp = DateTime.UtcNow
}));


app.Run();