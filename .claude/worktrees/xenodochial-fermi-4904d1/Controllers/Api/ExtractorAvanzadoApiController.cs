using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.ConfiguracionAdministrar)]
[Route("api/extractor-avanzado")]
public class ExtractorAvanzadoApiController : ControllerBase
{
    private readonly AppSettingsRepository _settings;
    private readonly ExtractorConfigurableService _extractor;

    public ExtractorAvanzadoApiController(
        AppSettingsRepository settings,
        ExtractorConfigurableService extractor)
    {
        _settings = settings;
        _extractor = extractor;
    }

    [HttpGet("configuracion")]
    public async Task<IActionResult> GetConfig()
    {
        try
        {
            return Ok(await _settings.GetExtractorAdvancedConfigAsync());
        }
        catch
        {
            return BadRequest("No se pudo cargar la configuracion del extractor.");
        }
    }

    [HttpPut("configuracion")]
    public async Task<IActionResult> SaveConfig(ExtractorAdvancedConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.PlantillaWhatsApp))
            return BadRequest("La plantilla de WhatsApp es requerida.");

        try
        {
            await _settings.SaveExtractorAdvancedConfigAsync(config);
            return NoContent();
        }
        catch
        {
            return BadRequest("No se pudo guardar la configuracion. Revisa los datos e intenta de nuevo.");
        }
    }

    [HttpPost("probar")]
    public async Task<IActionResult> Probar(ExtractorTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Asunto) && string.IsNullOrWhiteSpace(request.Cuerpo))
            return BadRequest("Ingresa un asunto o cuerpo de correo para probar.");

        try
        {
            return Ok(await _extractor.ProbarAsync(request));
        }
        catch
        {
            return BadRequest("No se pudo probar la extraccion. Revisa el texto del correo e intenta de nuevo.");
        }
    }
}
