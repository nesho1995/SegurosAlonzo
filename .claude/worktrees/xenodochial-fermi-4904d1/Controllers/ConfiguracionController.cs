using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;

namespace ReclamosWhatsApp.Controllers;

[Authorize(Policy = Permissions.ConfiguracionAdministrar)]
public class ConfiguracionController : Controller
{
    private readonly AppSettingsRepository _settings;

    public ConfiguracionController(AppSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<IActionResult> ExtractorCorreo()
    {
        var config = await _settings.GetCorreoExtractorConfigAsync();
        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtractorCorreo(CorreoExtractorConfig model)
    {
        ValidarRegex(model.SubjectPattern, nameof(model.SubjectPattern), requiereGruposAsunto: true);
        ValidarRegex(model.AseguradoPattern, nameof(model.AseguradoPattern));
        ValidarRegex(model.PolizaPattern, nameof(model.PolizaPattern));
        ValidarRegex(model.CaracteristicasPattern, nameof(model.CaracteristicasPattern));
        ValidarRegex(model.ConductorPattern, nameof(model.ConductorPattern));
        ValidarRegex(model.CelularPattern, nameof(model.CelularPattern));
        ValidarRegex(model.FechaPattern, nameof(model.FechaPattern));
        ValidarRegex(model.LugarPattern, nameof(model.LugarPattern));

        if (!ModelState.IsValid)
            return View(model);

        await _settings.SaveCorreoExtractorConfigAsync(model);
        TempData["Mensaje"] = "Configuración del extractor guardada.";

        return RedirectToAction(nameof(ExtractorCorreo));
    }

    private void ValidarRegex(string pattern, string field, bool requiereGruposAsunto = false)
    {
        try
        {
            var regex = new Regex(pattern);

            if (requiereGruposAsunto)
            {
                var groupNames = regex.GetGroupNames();
                if (!groupNames.Contains("placa") || !groupNames.Contains("reclamo"))
                    ModelState.AddModelError(field, "El patrón del asunto debe incluir grupos nombrados placa y reclamo.");
            }
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(field, "Regex inválido: " + ex.Message);
        }
    }
}
