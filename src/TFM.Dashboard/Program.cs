using MudBlazor.Services;
using TFM.Dashboard.Components;
using TFM.Dashboard.Extensions; // Suponiendo que tu método AddDashboardServices está aquí

var builder = WebApplication.CreateBuilder(args);

// ════════════════════════════════════════════════════════════
// 1. REGISTRO DE SERVICIOS
// ════════════════════════════════════════════════════════════

// Añade los servicios base para Blazor.
// .AddInteractiveServerComponents() habilita el renderizado del lado del servidor (Blazor Server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Añade los servicios de MudBlazor.
builder.Services.AddMudServices();

// Añade tus propios servicios del Dashboard.
// Asumo que tienes un método de extensión llamado AddDashboardServices.
builder.Services.AddDashboardServices();


// ════════════════════════════════════════════════════════════
// 2. CONSTRUCCIÓN DE LA APLICACIÓN
// ════════════════════════════════════════════════════════════

var app = builder.Build();


// ════════════════════════════════════════════════════════════
// 3. CONFIGURACIÓN DEL PIPELINE (MIDDLEWARE)
// El orden aquí es importante.
// ════════════════════════════════════════════════════════════

// Configuración para entornos que NO son de desarrollo.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // Activa HSTS para forzar el uso de HTTPS en producción.
    app.UseHsts();
}

// Redirige las peticiones HTTP a HTTPS.
app.UseHttpsRedirection();

// Permite que la aplicación sirva archivos estáticos desde la carpeta wwwroot (CSS, JS, imágenes).
app.UseStaticFiles();

// Añade la protección contra ataques CSRF (Cross-Site Request Forgery).
// Es obligatorio en .NET 8 para componentes interactivos.
app.UseAntiforgery();


// ════════════════════════════════════════════════════════════
// 4. MAPEO DE ENDPOINTS Y EJECUCIÓN
// ════════════════════════════════════════════════════════════

// Mapea el componente raíz de tu aplicación Blazor.
// La plantilla de .NET 8 usa 'App' como componente raíz que contiene el Router.
// Si has movido el Router a 'Routes.razor', debes usar app.MapRazorComponents<Routes>();
app.MapRazorComponents<App>();

// Mapea tu endpoint de health-check personalizado.
app.MapGet("/health", () => Results.Json(new
{
    status = "healthy",
    service = "dashboard",
    timestamp = DateTime.UtcNow
}));


// Inicia la aplicación.
app.Run();