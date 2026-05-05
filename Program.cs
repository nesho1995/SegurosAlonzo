using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using MySqlConnector;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;
using ReclamosWhatsApp.Services.DataQuality;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDev = builder.Environment.IsDevelopment();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Rate limiting: máx 10 uploads por minuto por IP
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync("{\"error\":\"Demasiadas solicitudes. Espera un momento antes de intentar de nuevo.\"}");
    };
});

// Auth setup
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = isDev
            ? CookieSecurePolicy.SameAsRequest   // local HTTP sin certificado
            : CookieSecurePolicy.Always;         // producción siempre HTTPS
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"error\":\"No has iniciado sesion.\"}");
            }

            context.Response.Redirect("/login");
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdRaw = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionId = context.Principal?.FindFirstValue("sid");
            if (!int.TryParse(userIdRaw, out var userId) || string.IsNullOrWhiteSpace(sessionId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var users = context.HttpContext.RequestServices.GetRequiredService<UserRepository>();
            var isValid = await users.TouchSessionAsync(
                userId,
                sessionId,
                DateTime.UtcNow.Add(options.ExpireTimeSpan));

            if (!isValid)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.Events.OnRedirectToAccessDenied = async context =>
        {
            var auditoria = context.HttpContext.RequestServices.GetService<AuditoriaService>();
            if (auditoria is not null)
            {
                await auditoria.RegistrarAsync(
                    "ACCESO_DENEGADO",
                    "SEGURIDAD",
                    null,
                    $"Acceso denegado a {context.Request.Path}");
            }

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"No tienes permiso para realizar esta accion.\"}");
                return;
            }

            context.Response.Redirect("/access-denied");
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim("perm", permission));
});

builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<ReclamoRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<CarteraRepository>();
builder.Services.AddScoped<AppSettingsRepository>();
builder.Services.AddScoped<RecordatorioRepository>();
builder.Services.AddScoped<PagoRepository>();
builder.Services.AddScoped<TallerRepository>();
builder.Services.AddScoped<DocumentoRepository>();
builder.Services.AddScoped<AuditoriaRepository>();
builder.Services.AddScoped<NotificacionRepository>();
builder.Services.AddScoped<AutomationRepository>();
builder.Services.AddScoped<EmpresaConfiguracionRepository>();
builder.Services.AddScoped<GastoRepository>();
builder.Services.AddScoped<CatalogoRepository>();
builder.Services.AddScoped<WhatsAppEnvioLogRepository>();
builder.Services.AddScoped<ReclamoPatronesRepository>();
builder.Services.AddScoped<PhoneNormalizationService>();
builder.Services.AddScoped<CarteraImportService>();
builder.Services.AddScoped<PolizaImportRulesService>();
builder.Services.AddScoped<TallerImportService>();
builder.Services.AddScoped<PlantillaCargaService>();
builder.Services.AddScoped<ReclamoExtractorService>();
builder.Services.AddScoped<ExtractorConfigurableService>();
builder.Services.AddScoped<MessageBuilderService>();
builder.Services.AddScoped<EmailReaderService>();
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<AutomationEngineService>();
builder.Services.AddScoped<AutomaticWhatsAppService>();
builder.Services.AddScoped<DocumentoStorageService>();
builder.Services.AddScoped<ReclamoPatronesService>();
builder.Services.AddScoped<ReclamoCorreoProcessingService>();
builder.Services.AddSingleton<PdfService>();
builder.Services.AddHostedService<EmailProcessingWorker>();
builder.Services.AddHostedService<StatusSyncWorker>();
//builder.Services.AddHostedService<AlertasWorker>();

var app = builder.Build();
var spaDistPath = Path.Combine(app.Environment.ContentRootPath, "ClientApp", "dist");
var spaIndexPath = Path.Combine(spaDistPath, "index.html");
var spaFiles = Directory.Exists(spaDistPath) ? new PhysicalFileProvider(spaDistPath) : null;

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = feature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        if (exception is not null)
            logger.LogError(exception, "Excepcion no manejada en {Path}", feature?.Path);
        var dbUnavailable = HasMySqlException(exception);
        context.Response.StatusCode = dbUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.ContentType = "application/json";
            if (dbUnavailable)
            {
                await context.Response.WriteAsync("{\"error\":\"La base de datos no esta disponible en este momento.\"}");
                return;
            }
            await context.Response.WriteAsync("{\"error\":\"Ocurrio un error interno.\"}");
            return;
        }

        if (File.Exists(spaIndexPath))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(spaIndexPath);
            return;
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"Ocurrio un error interno.\"}");
    });
});

static bool HasMySqlException(Exception? ex)
{
    while (ex is not null)
    {
        if (ex is MySqlException)
            return true;
        ex = ex.InnerException;
    }

    return false;
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection(); // en producción redirige HTTP → HTTPS automáticamente
}

if (spaFiles is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFiles });
}

app.UseStaticFiles();
app.UseRateLimiter();
app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Inicializar schema de pagos una sola vez al arranque
using (var scope = app.Services.CreateScope())
{
    var pagos = scope.ServiceProvider.GetRequiredService<PagoRepository>();
    await pagos.EnsureSchemaAsync();

    // Asegurar que las columnas opcionales existan antes de sincronizar estados
    var cartera = scope.ServiceProvider.GetRequiredService<CarteraRepository>();
    await cartera.EnsureImportSchemaAsync();

    // Sincronizar estados de pólizas vencidas al arrancar
    var actualizadas = await cartera.SincronizarEstadosPolizasAsync();
    if (actualizadas > 0)
        app.Logger.LogInformation("Startup: {Count} pólizas marcadas como VENCIDA por fecha.", actualizadas);


    // Sincronizar estado_negocio de clientes al arrancar
    var clientesActualizados = await cartera.SincronizarEstadosClientesAsync();
    if (clientesActualizados > 0)
        app.Logger.LogInformation("Startup: {Count} clientes con estado_negocio actualizado.", clientesActualizados);
}

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"Recurso no encontrado.\"}");
        return;
    }

    if (File.Exists(spaIndexPath))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(spaIndexPath);
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync("{\"error\":\"La interfaz no esta disponible. Ejecuta la compilacion del cliente antes de iniciar.\"}");
});

app.Run();
