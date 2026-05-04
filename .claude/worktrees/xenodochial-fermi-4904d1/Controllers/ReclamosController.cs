using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers;

[Authorize(Policy = Permissions.ReclamosVer)]
public class ReclamosController : Controller
{
    private readonly ReclamoRepository _repo;
    private readonly WhatsAppService _whatsApp;
    private readonly MessageBuilderService _messageBuilder;
    private readonly ReclamoExtractorService _extractor;
    private readonly AuditoriaService _auditoria;

    public ReclamosController(
        ReclamoRepository repo,
        WhatsAppService whatsApp,
        MessageBuilderService messageBuilder,
        ReclamoExtractorService extractor,
        AuditoriaService auditoria)
    {
        _repo = repo;
        _whatsApp = whatsApp;
        _messageBuilder = messageBuilder;
        _extractor = extractor;
        _auditoria = auditoria;
    }

    public async Task<IActionResult> Index(string? estado)
    {
        var reclamos = await _repo.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            reclamos = reclamos.Where(x => x.Estado == estado);
        }

        ViewBag.EstadoFiltro = estado;
        ViewBag.Stats = await _repo.GetDashboardStatsAsync();

        return View(reclamos);
    }

    public async Task<IActionResult> Details(int id)
    {
        var reclamo = await _repo.GetByIdAsync(id);
        if (reclamo is null) return NotFound();

        ViewBag.Documentos = await _repo.GetDocumentosAsync(id);
        return View(reclamo);
    }

    public IActionResult Create()
    {
        return View(new ReclamoWhatsApp
        {
            Estado = "PENDIENTE",
            FechaNotificacion = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> Create(ReclamoWhatsApp model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.MessageId = string.IsNullOrWhiteSpace(model.MessageId)
            ? Guid.NewGuid().ToString()
            : model.MessageId;

        model.Estado = string.IsNullOrWhiteSpace(model.Estado)
            ? "PENDIENTE"
            : model.Estado;

        model.Celular = NormalizarCelular(model.Celular);
        model.MensajeWhatsApp = await _messageBuilder.GenerateMessageAsync(model);

        var id = await _repo.InsertAsync(model);
        await _auditoria.RegistrarAsync("CREAR_RECLAMO", "RECLAMO", id, $"Reclamo creado: {model.Reclamo ?? model.Asunto ?? "Sin referencia"}");

        return RedirectToAction(nameof(Index));
    }

    public IActionResult ProbarExtractor()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> ProbarExtractor(string asunto, string cuerpo)
    {
        var email = new EmailMessageDto
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = asunto ?? "",
            Body = cuerpo ?? ""
        };

        var reclamo = await _extractor.ExtractAsync(email);
        reclamo.MensajeWhatsApp = await _messageBuilder.GenerateMessageAsync(reclamo);

        var id = await _repo.InsertAsync(reclamo);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DocumentosSubir)]
    public async Task<IActionResult> ActualizarDocumento(int documentoId, bool recibido, int reclamoId)
    {
        await _repo.ActualizarDocumentoAsync(documentoId, reclamoId, recibido);

        var completo = await _repo.TodosDocumentosRecibidosAsync(reclamoId);

        if (completo)
        {
            await _repo.UpdateEstadoAsync(reclamoId, "COMPLETO");

            var reclamo = await _repo.GetByIdAsync(reclamoId);

            if (reclamo is not null)
            {
                var notificacion = await _whatsApp.NotificarAdminReclamoCompletoAsync(reclamo);

                if (!notificacion.ok)
                    await _repo.UpdateEstadoAsync(reclamoId, "COMPLETO_ERROR_NOTIFICACION", notificacion.response);
            }
        }
        else
        {
            await _repo.UpdateEstadoAsync(reclamoId, "EN_SEGUIMIENTO");
        }

        return RedirectToAction(nameof(Details), new { id = reclamoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ReclamosEnviar)]
    public async Task<IActionResult> SubirDocumento(int documentoId, int reclamoId, IFormFile archivo)
    {
        if (archivo != null && archivo.Length > 0)
        {
            const long maxBytes = 15 * 1024 * 1024;
            if (archivo.Length > maxBytes)
            {
                TempData["Error"] = "El archivo supera el máximo permitido de 15 MB.";
                return RedirectToAction(nameof(Details), new { id = reclamoId });
            }

            var extensionesPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".jpg", ".jpeg", ".png"
            };

            var ext = Path.GetExtension(archivo.FileName);
            if (!extensionesPermitidas.Contains(ext))
            {
                TempData["Error"] = "Solo se permiten archivos PDF, JPG o PNG.";
                return RedirectToAction(nameof(Details), new { id = reclamoId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reclamos", reclamoId.ToString());
            Directory.CreateDirectory(uploadsFolder);
            
            var fileName = $"doc_{documentoId}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return await ActualizarDocumento(documentoId, true, reclamoId);
        }
        
        return RedirectToAction(nameof(Details), new { id = reclamoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> EnviarWhatsApp(int id)
    {
        var reclamo = await _repo.GetByIdAsync(id);
        if (reclamo is null) return NotFound();

        var result = await _whatsApp.SendTemplateAsync(reclamo);

        if (result.ok)
        {
            await _repo.UpdateEstadoAsync(id, "ENVIADO");
            await _auditoria.RegistrarAsync("ENVIAR_WHATSAPP", "RECLAMO", id, "WhatsApp enviado para reclamo.");
        }
        else
        {
            await _repo.UpdateEstadoAsync(id, "ERROR", result.response);
            await _auditoria.RegistrarAsync("ERROR_WHATSAPP", "RECLAMO", id, "Error enviando WhatsApp para reclamo.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private static string NormalizarCelular(string? celular)
    {
        if (string.IsNullOrWhiteSpace(celular))
            return "";

        var limpio = celular.Replace("-", "").Replace(" ", "").Replace("+", "").Trim();

        if (limpio.Length == 8)
            limpio = "504" + limpio;

        return limpio;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarRecordatorioManual(int id)
    {
        var reclamo = await _repo.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var documentos = await _repo.GetDocumentosAsync(id);
        var result = await _whatsApp.EnviarRecordatorioAsync(reclamo, documentos);

        if (result.ok)
        {
            await _repo.MarcarRecordatorioAsync(id);
            await _auditoria.RegistrarAsync("ENVIAR_RECORDATORIO", "RECLAMO", id, "Recordatorio manual enviado.");
        }
        else
        {
            await _repo.UpdateEstadoAsync(id, "ERROR", result.response);
            await _auditoria.RegistrarAsync("ERROR_RECORDATORIO", "RECLAMO", id, "Error enviando recordatorio manual.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
