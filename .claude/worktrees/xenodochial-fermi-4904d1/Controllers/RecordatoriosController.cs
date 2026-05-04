using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers;

[Authorize(Policy = Permissions.RecordatoriosVer)]
public class RecordatoriosController : Controller
{
    private readonly RecordatorioRepository _recordatorios;
    private readonly WhatsAppService _whatsApp;
    private readonly AppSettingsRepository _settings;
    private readonly AutomaticWhatsAppService _automaticWhatsApp;
    private readonly AuditoriaService _auditoria;

    public RecordatoriosController(RecordatorioRepository recordatorios, WhatsAppService whatsApp, AppSettingsRepository settings, AutomaticWhatsAppService automaticWhatsApp, AuditoriaService auditoria)
    {
        _recordatorios = recordatorios;
        _whatsApp = whatsApp;
        _settings = settings;
        _automaticWhatsApp = automaticWhatsApp;
        _auditoria = auditoria;
    }

    public async Task<IActionResult> Index(string? estado = "PENDIENTE", string? tipo = null, string? buscar = null, int pagina = 1, int pageSize = 25)
    {
        var filtro = new RecordatorioFiltro
        {
            Estado = estado,
            Tipo = tipo,
            Buscar = buscar,
            Pagina = pagina,
            PageSize = pageSize
        };

        var (items, total) = await _recordatorios.GetAsync(filtro);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)filtro.PageSize));

        var model = new RecordatorioBandejaViewModel
        {
            Items = items,
            Total = total,
            TotalPaginas = totalPaginas,
            Filtro = filtro,
            Stats = await _recordatorios.GetStatsAsync(),
            Tipos = await _recordatorios.GetTiposResumenAsync(estado)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Generar()
    {
        var creados = await _recordatorios.GenerarPendientesAsync(await _settings.GetEnvioAutomaticoConfigAsync());
        await _automaticWhatsApp.ProcesarRecordatoriosPendientesAsync();
        await _auditoria.RegistrarAsync("GENERAR_RECORDATORIOS", "RECORDATORIO", null, $"Recordatorios creados: {creados}.");
        TempData["Mensaje"] = $"Bandeja actualizada. Nuevos recordatorios creados: {creados}.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Editar(int id)
    {
        var recordatorio = await _recordatorios.GetByIdAsync(id);
        if (recordatorio is null)
            return NotFound();

        return View(recordatorio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Editar(int id, string asunto, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(mensaje))
        {
            TempData["Error"] = "El asunto y el mensaje son requeridos.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        await _recordatorios.UpdateMensajeAsync(id, asunto.Trim(), mensaje.Trim());
        await _auditoria.RegistrarAsync("EDITAR_RECORDATORIO", "RECORDATORIO", id, "Recordatorio editado.");
        TempData["Mensaje"] = "Recordatorio actualizado.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Descartar(int id)
    {
        await _recordatorios.MarcarDescartadoAsync(id);
        await _auditoria.RegistrarAsync("DESCARTAR_RECORDATORIO", "RECORDATORIO", id, "Recordatorio descartado.");
        TempData["Mensaje"] = "Recordatorio descartado.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Enviar(int id)
    {
        var recordatorio = await _recordatorios.GetByIdAsync(id);
        if (recordatorio is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(recordatorio.Telefono))
        {
            await _recordatorios.MarcarEnvioAsync(id, false, "El cliente no tiene teléfono configurado.");
            await _auditoria.RegistrarAsync("ERROR_RECORDATORIO", "RECORDATORIO", id, "No tiene telefono configurado.");
            TempData["Error"] = "El cliente no tiene teléfono configurado.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _automaticWhatsApp.EnviarRecordatorioAsync(recordatorio, automatico: false);

        TempData[result.ok ? "Mensaje" : "Error"] = result.ok
            ? "Recordatorio enviado."
            : "No se pudo enviar el recordatorio. Revisa el detalle del error.";

        return RedirectToAction(nameof(Index));
    }
}
