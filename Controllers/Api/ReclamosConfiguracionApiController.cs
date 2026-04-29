using MailKit.Net.Imap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.ConfiguracionAdministrar)]
[Route("api/reclamos-config")]
public class ReclamosConfiguracionApiController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppSettingsRepository _settings;
    private readonly ReclamoPatronesRepository _patrones;
    private readonly ReclamoPatronesService _patronesService;
    private readonly ReclamoCorreoProcessingService _processing;
    private readonly AuditoriaService _auditoria;

    public ReclamosConfiguracionApiController(
        IConfiguration configuration,
        AppSettingsRepository settings,
        ReclamoPatronesRepository patrones,
        ReclamoPatronesService patronesService,
        ReclamoCorreoProcessingService processing,
        AuditoriaService auditoria)
    {
        _configuration = configuration;
        _settings = settings;
        _patrones = patrones;
        _patronesService = patronesService;
        _processing = processing;
        _auditoria = auditoria;
    }

    [HttpGet("correo")]
    public async Task<IActionResult> GetCorreoConfig()
    {
        var config = await _settings.GetReclamoCorreoConfigAsync(_configuration);
        return Ok(new ReclamoCorreoConfigDto
        {
            EmailEnabled = config.EmailEnabled,
            WorkerEnabled = config.WorkerEnabled,
            Mailbox = config.Mailbox,
            MarkAsRead = config.MarkAsRead,
            LookbackHours = config.LookbackHours,
            Host = config.Host,
            Port = config.Port,
            UseSsl = config.UseSsl,
            Username = config.Username,
            PasswordMasked = string.IsNullOrWhiteSpace(config.Password) ? "" : "********"
        });
    }

    [HttpPut("correo")]
    public async Task<IActionResult> SaveCorreoConfig(ReclamoCorreoConfig config)
    {
        await _settings.SaveReclamoCorreoConfigAsync(config);
        await _auditoria.RegistrarAsync("GUARDAR_CONFIG_CORREO_RECLAMOS", "CONFIGURACION", 1, "Configuracion de correo de reclamos actualizada.");
        return NoContent();
    }

    [HttpGet("worker-estado")]
    public async Task<IActionResult> GetWorkerEstado()
    {
        return Ok(await _settings.GetReclamoWorkerEstadoAsync());
    }

    [HttpPost("probar-conexion")]
    public async Task<IActionResult> ProbarConexion()
    {
        var runtime = await _settings.GetReclamoCorreoConfigAsync(_configuration);
        if (string.IsNullOrWhiteSpace(runtime.Host) || string.IsNullOrWhiteSpace(runtime.Username) || string.IsNullOrWhiteSpace(runtime.Password))
            return BadRequest(new { error = "Completa host, usuario y password." });

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(runtime.Host, runtime.Port, runtime.UseSsl);
            await client.AuthenticateAsync(runtime.Username, runtime.Password);
            var folder = await client.GetFolderAsync(runtime.Mailbox ?? "INBOX");
            await folder.OpenAsync(MailKit.FolderAccess.ReadOnly);
            var count = folder.Count;
            await client.DisconnectAsync(true);
            return Ok(new { ok = true, mailbox = runtime.Mailbox, totalMensajes = count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"No se pudo conectar: {ex.Message}" });
        }
    }

    [HttpPost("probar-lectura")]
    public async Task<IActionResult> ProbarLectura()
    {
        var estado = await _processing.ProcessAsync();
        await _settings.SaveReclamoWorkerEstadoAsync(estado);
        return Ok(estado);
    }

    [HttpPost("procesar-ahora")]
    public async Task<IActionResult> ProcesarAhora()
    {
        var estado = await _processing.ProcessAsync();
        await _settings.SaveReclamoWorkerEstadoAsync(estado);
        await _auditoria.RegistrarAsync("PROCESAR_RECLAMOS_AHORA", "RECLAMO", null, $"Correos encontrados={estado.CorreosEncontrados}, procesados={estado.CorreosProcesados}");
        return Ok(estado);
    }

    [HttpPost("modo-recuperacion")]
    public async Task<IActionResult> ModoRecuperacion([FromQuery] int horas = 72)
    {
        horas = Math.Clamp(horas, 1, 24 * 30);
        var estado = await _processing.ProcessAsync(horas);
        await _settings.SaveReclamoWorkerEstadoAsync(estado);
        await _auditoria.RegistrarAsync("MODO_RECUPERACION_RECLAMOS", "RECLAMO", null, $"Recuperacion ejecutada {horas}h. Encontrados={estado.CorreosEncontrados}, procesados={estado.CorreosProcesados}");
        return Ok(estado);
    }

    [HttpGet("patrones")]
    public async Task<IActionResult> GetPatrones()
    {
        return Ok(new { items = await _patrones.GetPatronesAsync() });
    }

    [HttpPut("patrones")]
    public async Task<IActionResult> UpsertPatron(CorreoReclamoPatron model)
    {
        var id = await _patrones.UpsertPatronAsync(model);
        return Ok(new { id });
    }

    [HttpGet("plantillas")]
    public async Task<IActionResult> GetPlantillas()
    {
        return Ok(new { items = await _patrones.GetPlantillasAsync() });
    }

    [HttpPut("plantillas")]
    public async Task<IActionResult> UpsertPlantilla(CorreoReclamoPlantilla model)
    {
        var id = await _patrones.UpsertPlantillaAsync(model);
        return Ok(new { id });
    }

    [HttpPut("plantillas/{plantillaId:int}/reglas")]
    public async Task<IActionResult> SetPlantillaReglas(int plantillaId, [FromBody] int[] patronIds)
    {
        await _patrones.SetPlantillaReglasAsync(plantillaId, patronIds ?? Array.Empty<int>());
        return NoContent();
    }

    [HttpGet("plantillas/{plantillaId:int}/reglas")]
    public async Task<IActionResult> GetPlantillaReglas(int plantillaId)
    {
        return Ok(new { items = await _patrones.GetPlantillaReglasAsync(plantillaId) });
    }

    [HttpGet("plantillas/{plantillaId:int}/condiciones")]
    public async Task<IActionResult> GetCondiciones(int plantillaId)
    {
        return Ok(new { items = await _patrones.GetCondicionesAsync(plantillaId) });
    }

    [HttpPut("condiciones")]
    public async Task<IActionResult> UpsertCondicion(CorreoReclamoCondicion model)
    {
        var id = await _patrones.UpsertCondicionAsync(model);
        return Ok(new { id });
    }

    [HttpPost("probar-patrones")]
    public async Task<IActionResult> ProbarPatrones(ProbarPatronesRequest request)
    {
        return Ok(await _patronesService.ProbarExtraccionAsync(request));
    }
}
