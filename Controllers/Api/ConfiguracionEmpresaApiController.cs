using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ConfiguracionEmpresaApiController(EmpresaConfiguracionRepository repo, AppSettingsRepository settings, AuditoriaService auditoria, IWebHostEnvironment env)
    {
        _repo = repo;
        _settings = settings;
        _auditoria = auditoria;
        _env = env;
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

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
