using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MySqlConnector;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/configuracion/empresa")]
public class ConfiguracionEmpresaApiController : ControllerBase
{
    private static readonly HashSet<string> LogoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
    private readonly EmpresaConfiguracionRepository _repo;
    private readonly AppSettingsRepository _settings;
    private readonly AuditoriaService _auditoria;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly WhatsAppService _whatsApp;

    public ConfiguracionEmpresaApiController(
        EmpresaConfiguracionRepository repo,
        AppSettingsRepository settings,
        AuditoriaService auditoria,
        IWebHostEnvironment env,
        IConfiguration configuration,
        WhatsAppService whatsApp)
    {
        _repo = repo;
        _settings = settings;
        _auditoria = auditoria;
        _env = env;
        _configuration = configuration;
        _whatsApp = whatsApp;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        EmpresaConfiguracion config;
        try
        {
            config = await _repo.GetAsync();
        }
        catch (MySqlException)
        {
            config = new EmpresaConfiguracion
            {
                Id = 1,
                NombreEmpresa = "Seguros Alonzo",
                ColorPrimario = "#2563eb"
            };
        }

        config.LogoUrl = string.IsNullOrWhiteSpace(config.LogoRuta) ? null : "/api/configuracion/empresa/logo";
        return Ok(config);
    }

    [HttpPut]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> Update(EmpresaConfiguracion config)
    {
        if (string.IsNullOrWhiteSpace(config.NombreEmpresa))
            return BadRequest(new { error = "El nombre de la empresa es obligatorio." });

        await _repo.UpdateAsync(config, CurrentUserId());
        await _auditoria.RegistrarAsync("CAMBIAR_CONFIGURACION_EMPRESA", "CONFIGURACION", 1, "Configuracion de empresa actualizada.");
        return NoContent();
    }

    [HttpPost("logo")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadLogo([FromForm] IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Selecciona un logo." });
        if (archivo.Length > 2 * 1024 * 1024)
            return BadRequest(new { error = "El logo no debe superar 2 MB." });

        var extension = Path.GetExtension(archivo.FileName);
        if (!LogoExtensions.Contains(extension))
            return BadRequest(new { error = "El logo debe ser PNG, JPG, JPEG o WEBP." });

        var relativeFolder = Path.Combine("storage", "configuracion", "empresa");
        var absoluteFolder = Path.Combine(_env.ContentRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);
        var safeName = $"logo-{DateTime.UtcNow:yyyyMMddHHmmss}{extension.ToLowerInvariant()}";
        var relativePath = Path.Combine(relativeFolder, safeName);
        var absolutePath = Path.Combine(_env.ContentRootPath, relativePath);

        await using (var stream = new FileStream(absolutePath, FileMode.CreateNew))
            await archivo.CopyToAsync(stream);

        await _repo.UpdateLogoAsync(relativePath, CurrentUserId());
        await _auditoria.RegistrarAsync("CAMBIAR_LOGO_EMPRESA", "CONFIGURACION", 1, "Logo de empresa actualizado.");
        return Ok(new { logoUrl = "/api/configuracion/empresa/logo" });
    }

    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> Logo()
    {
        var config = await _repo.GetAsync();
        if (string.IsNullOrWhiteSpace(config.LogoRuta))
            return NotFound();

        var absolutePath = Path.Combine(_env.ContentRootPath, config.LogoRuta);
        if (!System.IO.File.Exists(absolutePath))
            return NotFound();

        var contentType = Path.GetExtension(absolutePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        return PhysicalFile(absolutePath, contentType);
    }

    [HttpGet("/api/configuracion/envios")]
    [Authorize]
    public async Task<IActionResult> GetEnvios()
    {
        return Ok(await _settings.GetEnvioAutomaticoConfigAsync());
    }

    [HttpPut("/api/configuracion/envios")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> UpdateEnvios(EnvioAutomaticoConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.PlantillaPagoProximo)
            || string.IsNullOrWhiteSpace(config.PlantillaPagoVencido)
            || string.IsNullOrWhiteSpace(config.PlantillaPolizaPorVencer)
            || string.IsNullOrWhiteSpace(config.PlantillaPolizaVencida)
            || string.IsNullOrWhiteSpace(config.PlantillaReclamo))
        {
            return BadRequest(new { error = "Completa todas las plantillas antes de guardar." });
        }

        await _settings.SaveEnvioAutomaticoConfigAsync(config);
        await _auditoria.RegistrarAsync(
            "CAMBIAR_AUTOMATIZACION_ENVIOS",
            "CONFIGURACION",
            1,
            $"Configuracion de automatizacion actualizada. Reclamos={(config.AutoEnviarReclamos ? "ACTIVO" : "INACTIVO")}, Pagos={(config.AutoEnviarRecordatoriosPago ? "ACTIVO" : "INACTIVO")}, Polizas={(config.AutoEnviarRecordatoriosPoliza ? "ACTIVO" : "INACTIVO")}.");
        return NoContent();
    }

    [HttpGet("/api/configuracion/whatsapp")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> GetWhatsApp()
    {
        return Ok(await _settings.GetWhatsAppConfigAsync(_configuration));
    }

    [HttpPut("/api/configuracion/whatsapp")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> UpdateWhatsApp(WhatsAppConfig config)
    {
        if (config.Enabled && string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return BadRequest(new { error = "Para activar WhatsApp debes indicar el Phone Number ID." });

        await _settings.SaveWhatsAppConfigAsync(config, _configuration);
        await _auditoria.RegistrarAsync(
            "CAMBIAR_CONFIGURACION_WHATSAPP",
            "CONFIGURACION",
            1,
            $"WhatsApp produccion {(config.Enabled ? "activado" : "desactivado")}.");
        return NoContent();
    }

    [HttpPost("/api/configuracion/whatsapp/probar")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> ProbarWhatsApp(WhatsAppTestRequest request)
    {
        var config = await _settings.GetWhatsAppConfigAsync(_configuration);
        var telefono = string.IsNullOrWhiteSpace(request.Telefono) ? config.AdminWhatsAppNumber : request.Telefono;
        if (string.IsNullOrWhiteSpace(telefono))
            return BadRequest(new { error = "Indica un telefono de prueba o configura el numero administrador." });

        var mensaje = string.IsNullOrWhiteSpace(request.Mensaje)
            ? "Prueba de WhatsApp desde Seguros Alonzo."
            : request.Mensaje.Trim();

        var result = await _whatsApp.SendConfiguredMessageAsync(telefono, mensaje);
        await _auditoria.RegistrarAsync(
            result.ok ? "PROBAR_WHATSAPP_OK" : "PROBAR_WHATSAPP_ERROR",
            "CONFIGURACION",
            1,
            result.response);
        return Ok(new { result.ok, result.response });
    }

    [HttpGet("/api/configuracion/smtp")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> GetSmtp()
    {
        var config = await _settings.GetSmtpConfigAsync(_configuration);
        return Ok(new SmtpConfigDto
        {
            Enabled = config.Enabled,
            Host = config.Host,
            Port = config.Port,
            UseSsl = config.UseSsl,
            Username = config.Username,
            PasswordMasked = string.IsNullOrWhiteSpace(config.Password) ? "" : "********",
            FromAddress = config.FromAddress,
            FromName = config.FromName,
            InternalCopyEmails = config.InternalCopyEmails
        });
    }

    [HttpPut("/api/configuracion/smtp")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> UpdateSmtp(SmtpConfig config)
    {
        if (config.Enabled && (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.Username)))
            return BadRequest(new { error = "Completa host y correo de produccion antes de activar SMTP." });
        var invalidCopy = SplitEmailList(config.InternalCopyEmails).FirstOrDefault(x => !MailboxAddress.TryParse(x, out _));
        if (!string.IsNullOrWhiteSpace(invalidCopy))
            return BadRequest(new { error = $"Correo interno de copia no valido: {invalidCopy}" });

        await _settings.SaveSmtpConfigAsync(config, _configuration);
        await _auditoria.RegistrarAsync("CAMBIAR_CONFIGURACION_SMTP", "CONFIGURACION", 1, $"SMTP produccion {(config.Enabled ? "activado" : "desactivado")}.");
        return NoContent();
    }

    [HttpPost("/api/configuracion/smtp/probar")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> ProbarSmtp()
    {
        var config = await _settings.GetSmtpConfigAsync(_configuration);
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.Password))
            return BadRequest(new { error = "Completa host, correo de produccion y password SMTP." });

        try
        {
            using var client = new SmtpClient();
            var secure = config.Port == 465 || config.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(config.Host, config.Port, secure);
            await client.AuthenticateAsync(config.Username, config.Password);
            await client.DisconnectAsync(true);
            await _auditoria.RegistrarAsync("PROBAR_SMTP_OK", "CONFIGURACION", 1, "Conexion SMTP verificada.");
            return Ok(new { ok = true, response = "Conexion SMTP exitosa." });
        }
        catch (Exception ex)
        {
            await _auditoria.RegistrarAsync("PROBAR_SMTP_ERROR", "CONFIGURACION", 1, ex.Message);
            return BadRequest(new { error = $"No se pudo conectar a SMTP: {ex.Message}" });
        }
    }

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private static IEnumerable<string> SplitEmailList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
