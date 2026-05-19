using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.ReclamosVer)]
[Route("api/reclamos")]
public class ReclamosApiController : ControllerBase
{
    private readonly ReclamoRepository _reclamos;
    private readonly DocumentoRepository _documentos;
    private readonly WhatsAppService _whatsApp;
    private readonly EmailSenderService _emailSender;
    private readonly AuditoriaService _auditoria;

    public ReclamosApiController(ReclamoRepository reclamos, DocumentoRepository documentos, WhatsAppService whatsApp, EmailSenderService emailSender, AuditoriaService auditoria)
    {
        _reclamos = reclamos;
        _documentos = documentos;
        _whatsApp = whatsApp;
        _emailSender = emailSender;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string? estado = null, string? buscar = null)
    {
        var items = await _reclamos.GetAllAsync();

        var specialEstado = (estado ?? "TODOS").Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(estado)
            && specialEstado != "TODOS"
            && specialEstado is not ("PENDIENTES_PAGO" or "SIN_RESPUESTA_ASEGURADORA" or "CON_RESPUESTA_ASEGURADORA" or "SIN_TELEFONO" or "SIN_POLIZA" or "NO_REVISADO" or "EN_REVISION" or "ESPERANDO_CLIENTE" or "ESPERANDO_ASEGURADORA" or "LISTO"))
        {
            items = items.Where(x => (x.EstadoReclamo ?? x.Estado) == estado);
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var text = buscar.Trim();
            var compact = NormalizeSearch(text);
            items = items.Where(x =>
                (x.Conductor ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Asegurado ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Celular ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.CiudadDetectada ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.LugarAccidente ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Descripcion ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Poliza ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Placa ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.NumeroReclamo ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Reclamo ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || NormalizeSearch(x.Celular).Contains(compact)
                || NormalizeSearch(x.Placa).Contains(compact)
                || NormalizeSearch(x.Poliza).Contains(compact)
                || NormalizeSearch(x.Reclamo).Contains(compact)
                || NormalizeSearch(x.NumeroReclamo).Contains(compact));
        }

        var enriched = new List<object>();
        foreach (var item in items)
        {
            var documentos = (await _reclamos.GetDocumentosAsync(item.Id)).ToList();
            var faltantes = documentos.Count(x => !x.Recibido);
            var pagosPendientes = documentos.Count(x => !x.Recibido && IsComprobanteFinalName(x.Documento));
            var estadoSeguimiento = string.IsNullOrWhiteSpace(item.EstadoSeguimiento) ? "NO_REVISADO" : item.EstadoSeguimiento;

            if (specialEstado == "PENDIENTES_PAGO" && pagosPendientes == 0) continue;
            if (specialEstado == "SIN_RESPUESTA_ASEGURADORA" && !string.IsNullOrWhiteSpace(item.RespuestaAseguradora)) continue;
            if (specialEstado == "CON_RESPUESTA_ASEGURADORA" && string.IsNullOrWhiteSpace(item.RespuestaAseguradora)) continue;
            if (specialEstado == "SIN_TELEFONO" && !string.IsNullOrWhiteSpace(item.Celular)) continue;
            if (specialEstado == "SIN_POLIZA" && !string.IsNullOrWhiteSpace(item.Poliza)) continue;
            if (specialEstado is "NO_REVISADO" or "EN_REVISION" or "ESPERANDO_CLIENTE" or "ESPERANDO_ASEGURADORA" or "LISTO"
                && !string.Equals(estadoSeguimiento, specialEstado, StringComparison.OrdinalIgnoreCase)) continue;

            enriched.Add(new
            {
                item.Id,
                item.Asunto,
                item.Aseguradora,
                item.Asegurado,
                item.Poliza,
                item.Placa,
                item.Reclamo,
                item.Conductor,
                item.Celular,
                item.FechaNotificacion,
                item.LugarAccidente,
                Estado = item.EstadoReclamo ?? item.Estado,
                item.TipoReclamo,
                item.NumeroReclamo,
                item.MontoEstimado,
                item.MontoAprobado,
                item.MontoPagado,
                item.FechaCreacion,
                item.CiudadDetectada,
                item.TallerSugeridoId,
                item.TallerAsignadoId,
                item.MotivoSugerenciaTaller,
                item.CorreoAseguradoraPrincipal,
                item.CorreoAseguradoraCopia,
                item.RespuestaAseguradora,
                item.FechaRespuestaAseguradora,
                item.AseguradoraAprobado,
                EstadoSeguimiento = estadoSeguimiento,
                item.FechaUltimaRevision,
                item.UsuarioUltimaRevisionId,
                item.Descripcion,
                DocumentosPendientes = faltantes,
                PagosPendientes = pagosPendientes
            });
        }

        return Ok(new { items = enriched.Take(100).ToList(), total = enriched.Count });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> Create([FromBody] ReclamoWhatsApp model)
    {
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? "NUEVO" : model.Estado.Trim().ToUpperInvariant();
        model.EstadoReclamo = model.Estado;
        model.TipoReclamo = string.IsNullOrWhiteSpace(model.TipoReclamo) ? "GENERAL" : model.TipoReclamo.Trim().ToUpperInvariant();
        model.FechaReclamo = model.FechaReclamo ?? model.FechaNotificacion ?? DateTime.Today;
        model.ActualizadoEn = DateTime.UtcNow;
        var id = await _reclamos.InsertAsync(model);
        var faltantes = await _reclamos.CountFaltantesByEntidadDocumentosAsync(id, model.TipoReclamo);
        if (faltantes > 0)
            await _reclamos.UpdateEstadoAsync(id, "DOCUMENTOS_PENDIENTES");
        await _auditoria.RegistrarAsync("CREAR_RECLAMO", "RECLAMO", id, $"Reclamo creado. Tipo={model.TipoReclamo}.");
        return Ok(new { id, documentosPendientes = faltantes });
    }

    [HttpGet("{id:int}/checklist")]
    public async Task<IActionResult> GetChecklist(int id, [FromQuery] string? tipoReclamo = null)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();
        var tipo = string.IsNullOrWhiteSpace(tipoReclamo) ? reclamo.TipoReclamo ?? "GENERAL" : tipoReclamo!;
        var requisitos = await _reclamos.GetRequisitosByTipoAsync(tipo);
        var faltantes = await _reclamos.CountFaltantesByEntidadDocumentosAsync(id, tipo);
        return Ok(new { tipoReclamo = tipo, requisitos, documentosPendientes = faltantes });
    }

    [HttpGet("{id:int}/documentos-pendientes")]
    public async Task<IActionResult> GetDocumentosPendientes(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        await _reclamos.CrearDocumentosInicialesAsync(id);
        var documentos = await _reclamos.GetDocumentosAsync(id);
        return Ok(new { items = documentos, pendientes = documentos.Count(x => !x.Recibido) });
    }

    [HttpPut("{id:int}/documentos/{documentoId:int}")]
    [Authorize(Policy = Permissions.DocumentosSubir)]
    public async Task<IActionResult> ActualizarDocumentoPendiente(int id, int documentoId, [FromBody] ActualizarDocumentoReclamoRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var yaEstabaCompleto = await _reclamos.TodosDocumentosRecibidosAsync(id);
        await _reclamos.ActualizarDocumentoAsync(documentoId, id, request.Recibido);
        var completo = await _reclamos.TodosDocumentosRecibidosAsync(id);
        await _reclamos.UpdateEstadoAsync(id, completo ? "COMPLETO" : "EN_SEGUIMIENTO");
        if (completo && !yaEstabaCompleto)
        {
            var result = await _whatsApp.EnviarDocumentosRecibidosAsync(reclamo);
            await _auditoria.RegistrarAsync(result.ok ? "AVISO_DOCUMENTOS_RECIBIDOS" : "ERROR_AVISO_DOCUMENTOS_RECIBIDOS", "RECLAMO", id, result.ok ? "Cliente notificado por documentos completos." : result.response);
            if (!string.IsNullOrWhiteSpace(reclamo.CorreoAseguradoraPrincipal))
            {
                var documentos = (await _documentos.GetByEntidadAsync("RECLAMO", id)).ToList();
                var soloFinales = IsPostApprovalStage(reclamo);
                var documentosEnviar = soloFinales ? documentos.Where(IsComprobanteFinal).ToList() : documentos;
                var correo = await _emailSender.EnviarDocumentosReclamoAsync(reclamo, reclamo.CorreoAseguradoraPrincipal, documentosEnviar, reclamo.CorreoAseguradoraCopia, soloFinales);
                await _auditoria.RegistrarAsync(correo.ok ? "ENVIAR_DOCUMENTOS_ASEGURADORA_AUTO" : "ERROR_DOCUMENTOS_ASEGURADORA_AUTO", "RECLAMO", id, correo.ok ? $"Documentos enviados a {reclamo.CorreoAseguradoraPrincipal}." : correo.response);
            }
        }
        await _auditoria.RegistrarAsync(
            "ACTUALIZAR_DOCUMENTO_RECLAMO",
            "RECLAMO",
            id,
            $"Documento {documentoId} marcado como {(request.Recibido ? "recibido" : "pendiente")}.");

        return NoContent();
    }

    [HttpPost("requisitos")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> UpsertRequisito([FromBody] ReclamoRequisito model)
    {
        var id = await _reclamos.UpsertRequisitoAsync(model);
        await _auditoria.RegistrarAsync("RECLAMO_REQUISITO_UPSERT", "RECLAMO_REQUISITO", id, $"{model.TipoReclamo}:{model.TipoDocumento} requerido={model.Requerido}");
        return Ok(new { id });
    }

    [HttpPost("{id:int}/solicitar-documentos")]
    [Authorize(Policy = Permissions.ReclamosEnviar)]
    public async Task<IActionResult> SolicitarDocumentosFaltantes(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();
        await _reclamos.CrearDocumentosInicialesAsync(id);
        await _reclamos.UpdateEstadoAsync(id, "DOCUMENTOS_PENDIENTES");
        await _auditoria.RegistrarAsync("SOLICITAR_DOCUMENTOS_RECLAMO", "RECLAMO", id, "Se solicito documentos faltantes.");
        return NoContent();
    }

    [HttpPost("{id:int}/marcar-documentos-completos")]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> MarcarDocumentosCompletos(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();
        await _reclamos.CrearDocumentosInicialesAsync(id);
        if (await _reclamos.TodosDocumentosRecibidosAsync(id))
        {
            await _auditoria.RegistrarAsync("DOCUMENTOS_COMPLETOS_SIN_ENVIO", "RECLAMO", id, "Se evito reenvio porque el reclamo ya tenia todos los documentos recibidos.");
            return Ok(new { ok = false, response = "Este reclamo ya estaba marcado con todos los documentos recibidos. No se reenvio WhatsApp." });
        }

        await _reclamos.MarcarTodosDocumentosAsync(id, recibido: true);
        await _reclamos.UpdateEstadoAsync(id, "DOCUMENTOS_COMPLETOS");
        var result = await _whatsApp.EnviarDocumentosRecibidosAsync(reclamo);
        await _auditoria.RegistrarAsync(result.ok ? "AVISO_DOCUMENTOS_RECIBIDOS" : "ERROR_AVISO_DOCUMENTOS_RECIBIDOS", "RECLAMO", id, result.ok ? "Cliente notificado por documentos completos." : result.response);
        if (!string.IsNullOrWhiteSpace(reclamo.CorreoAseguradoraPrincipal))
        {
            var documentos = (await _documentos.GetByEntidadAsync("RECLAMO", id)).ToList();
            var soloFinales = IsPostApprovalStage(reclamo);
            var documentosEnviar = soloFinales ? documentos.Where(IsComprobanteFinal).ToList() : documentos;
            var correo = await _emailSender.EnviarDocumentosReclamoAsync(reclamo, reclamo.CorreoAseguradoraPrincipal, documentosEnviar, reclamo.CorreoAseguradoraCopia, soloFinales);
            await _auditoria.RegistrarAsync(correo.ok ? "ENVIAR_DOCUMENTOS_ASEGURADORA_AUTO" : "ERROR_DOCUMENTOS_ASEGURADORA_AUTO", "RECLAMO", id, correo.ok ? $"Documentos enviados a {reclamo.CorreoAseguradoraPrincipal}." : correo.response);
            result = correo.ok
                ? (true, soloFinales ? "Documentos completos. Cliente notificado y comprobantes finales enviados a aseguradora." : "Documentos completos. Cliente notificado y expediente enviado a aseguradora.")
                : (false, $"Documentos completos, pero no se pudo enviar a aseguradora: {correo.response}");
        }
        await _auditoria.RegistrarAsync("MARCAR_DOCUMENTOS_COMPLETOS_RECLAMO", "RECLAMO", id, "Documentos marcados como completos.");
        return Ok(new { result.ok, result.response });
    }

    [HttpPut("{id:int}/correos-aseguradora")]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> UpdateCorreosAseguradora(int id, [FromBody] CorreosAseguradoraRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        await _reclamos.UpdateCorreosAseguradoraAsync(id, request.Principal, request.Copia);
        await _auditoria.RegistrarAsync("ACTUALIZAR_CORREOS_ASEGURADORA", "RECLAMO", id, "Correos de aseguradora actualizados.");
        return NoContent();
    }

    [HttpPut("{id:int}/datos-basicos")]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> UpdateDatosBasicos(int id, [FromBody] ReclamoDatosBasicosRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Poliza)
            && string.IsNullOrWhiteSpace(request.Reclamo)
            && string.IsNullOrWhiteSpace(request.Placa))
            return BadRequest(new { message = "Debe conservar al menos poliza, reclamo o placa para identificar el expediente." });

        await _reclamos.UpdateDatosBasicosAsync(id, request.Poliza, request.Reclamo, request.Placa, request.Celular, request.Ciudad);
        await _auditoria.RegistrarAsync("ACTUALIZAR_DATOS_BASICOS_RECLAMO", "RECLAMO", id, $"Datos actualizados. Poliza={request.Poliza}; Reclamo={request.Reclamo}; Placa={request.Placa}; Ciudad={request.Ciudad}.");
        return NoContent();
    }

    [HttpGet("{id:int}/respuestas-aseguradora")]
    public async Task<IActionResult> GetRespuestasAseguradora(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var items = await _reclamos.GetRespuestasAseguradoraAsync(id);
        return Ok(new { items });
    }

    [HttpPut("{id:int}/seguimiento")]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> UpdateSeguimiento(int id, [FromBody] ReclamoSeguimientoRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        await _reclamos.UpdateEstadoSeguimientoAsync(id, request.EstadoSeguimiento, CurrentUserId());
        await _auditoria.RegistrarAsync("ACTUALIZAR_SEGUIMIENTO_RECLAMO", "RECLAMO", id, $"Seguimiento={request.EstadoSeguimiento}.");
        return NoContent();
    }

    [HttpGet("siguiente-pendiente")]
    public async Task<IActionResult> GetSiguientePendiente([FromQuery] int? actualId = null)
    {
        var item = await _reclamos.GetSiguientePendienteAsync(actualId);
        return Ok(new { item });
    }

    [HttpPost("{id:int}/documentos/{documentoId:int}/excepcion")]
    [Authorize(Policy = Permissions.DocumentosSubir)]
    public async Task<IActionResult> AceptarDocumentoConExcepcion(int id, int documentoId, [FromBody] DocumentoExcepcionRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var yaEstabaCompleto = await _reclamos.TodosDocumentosRecibidosAsync(id);
        var result = await _reclamos.AceptarDocumentoConExcepcionAsync(documentoId, id, request.Observacion);
        if (!result.ok)
            return BadRequest(new { error = result.response });

        var completo = await _reclamos.TodosDocumentosRecibidosAsync(id);
        await _reclamos.UpdateEstadoAsync(id, completo ? "COMPLETO" : "EN_SEGUIMIENTO");
        if (completo && !yaEstabaCompleto)
        {
            var aviso = await _whatsApp.EnviarDocumentosRecibidosAsync(reclamo);
            await _auditoria.RegistrarAsync(aviso.ok ? "AVISO_DOCUMENTOS_RECIBIDOS" : "ERROR_AVISO_DOCUMENTOS_RECIBIDOS", "RECLAMO", id, aviso.ok ? "Cliente notificado por documentos completos." : aviso.response);
            if (!string.IsNullOrWhiteSpace(reclamo.CorreoAseguradoraPrincipal))
            {
                var documentos = (await _documentos.GetByEntidadAsync("RECLAMO", id)).ToList();
                var soloFinales = IsPostApprovalStage(reclamo);
                var documentosEnviar = soloFinales ? documentos.Where(IsComprobanteFinal).ToList() : documentos;
                var correo = await _emailSender.EnviarDocumentosReclamoAsync(reclamo, reclamo.CorreoAseguradoraPrincipal, documentosEnviar, reclamo.CorreoAseguradoraCopia, soloFinales);
                await _auditoria.RegistrarAsync(correo.ok ? "ENVIAR_DOCUMENTOS_ASEGURADORA_AUTO" : "ERROR_DOCUMENTOS_ASEGURADORA_AUTO", "RECLAMO", id, correo.ok ? $"Documentos enviados a {reclamo.CorreoAseguradoraPrincipal}." : correo.response);
            }
        }

        await _auditoria.RegistrarAsync("ACEPTAR_DOCUMENTO_EXCEPCION", "RECLAMO", id, $"Documento {documentoId} aceptado con excepcion: {request.Observacion}");
        return Ok(new { result.ok, result.response, completo });
    }

    [HttpPost("{id:int}/enviar-aseguradora")]
    [Authorize(Policy = Permissions.ReclamosEnviar)]
    public async Task<IActionResult> EnviarAseguradora(int id, [FromBody] EnviarAseguradoraRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var documentos = (await _documentos.GetByEntidadAsync("RECLAMO", id)).ToList();
        var destino = string.IsNullOrWhiteSpace(request.CorreoAseguradora) ? reclamo.CorreoAseguradoraPrincipal : request.CorreoAseguradora;
        var copia = string.IsNullOrWhiteSpace(request.CorreoCopia) ? reclamo.CorreoAseguradoraCopia : request.CorreoCopia;
        var soloFinales = IsPostApprovalStage(reclamo);
        var documentosEnviar = soloFinales ? documentos.Where(IsComprobanteFinal).ToList() : documentos;
        var result = await _emailSender.EnviarDocumentosReclamoAsync(reclamo, destino ?? "", documentosEnviar, copia, soloFinales);
        await _auditoria.RegistrarAsync(result.ok ? "ENVIAR_DOCUMENTOS_ASEGURADORA" : "ERROR_ENVIAR_DOCUMENTOS_ASEGURADORA", "RECLAMO", id, result.ok ? $"Documentos enviados a {destino}." : result.response);
        return Ok(new { result.ok, result.response });
    }

    [HttpPost("{id:int}/enviar-whatsapp")]
    [Authorize(Policy = Permissions.ReclamosEnviar)]
    public async Task<IActionResult> EnviarWhatsApp(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var result = await _whatsApp.SendTemplateAsync(reclamo);
        await _reclamos.UpdateEstadoAsync(id, result.ok ? "ENVIADO" : "ERROR", result.ok ? null : result.response);
        await _auditoria.RegistrarAsync(result.ok ? "ENVIAR_WHATSAPP" : "ERROR_WHATSAPP", "RECLAMO", id, result.ok ? "WhatsApp manual enviado para reclamo." : "Error enviando WhatsApp manual para reclamo.");
        return Ok(new { result.ok, result.response });
    }

    [HttpPost("{id:int}/enviar-recordatorio")]
    [Authorize(Policy = Permissions.ReclamosEnviar)]
    public async Task<IActionResult> EnviarRecordatorioManual(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        await _reclamos.CrearDocumentosInicialesAsync(id);
        var documentos = await _reclamos.GetDocumentosAsync(id);
        var result = await _whatsApp.EnviarRecordatorioAsync(reclamo, documentos);
        if (result.ok)
        {
            await _reclamos.MarcarRecordatorioAsync(id);
            await _auditoria.RegistrarAsync("ENVIAR_RECORDATORIO", "RECLAMO", id, "Recordatorio manual enviado.");
        }
        else if (result.response.StartsWith("No hay documentos pendientes", StringComparison.OrdinalIgnoreCase))
        {
            await _auditoria.RegistrarAsync("RECORDATORIO_NO_APLICA", "RECLAMO", id, result.response);
        }
        else
        {
            await _reclamos.UpdateEstadoAsync(id, "ERROR", result.response);
            await _auditoria.RegistrarAsync("ERROR_RECORDATORIO", "RECLAMO", id, "Error enviando recordatorio manual.");
        }

        return Ok(new { result.ok, result.response });
    }

    [HttpPost("{id:int}/reprocesar-correo")]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> ReprocesarCorreo(int id)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();
        await _auditoria.RegistrarAsync("REPROCESAR_CORREO_RECLAMO", "RECLAMO", id, "Se solicito reproceso manual del correo.");
        return Ok(new { message = "Reproceso registrado para seguimiento." });
    }

    [HttpPost("{id:int}/respuesta-aseguradora")]
    [Authorize(Policy = Permissions.ReclamosEditar)]
    public async Task<IActionResult> RegistrarRespuestaAseguradora(int id, [FromBody] RespuestaAseguradoraRequest request)
    {
        var reclamo = await _reclamos.GetByIdAsync(id);
        if (reclamo is null)
            return NotFound();

        var analysis = InsuranceResponseAnalyzer.Analyze(request.Respuesta);
        var aprobado = request.Aprobado || analysis.Aprobado;
        await _reclamos.RegistrarRespuestaAseguradoraAsync(id, request.Respuesta, aprobado);
        await _reclamos.RegistrarRespuestaAseguradoraHistorialAsync(
            id,
            "MANUAL",
            null,
            null,
            request.Respuesta,
            analysis with { Aprobado = aprobado },
            null,
            CurrentUserId());
        if (aprobado)
        {
            if (analysis.RequiereRsa)
                await _reclamos.AgregarDocumentoPendienteSiNoExisteAsync(id, "Comprobante de pago de RSA");
            if (analysis.RequiereDeducible)
                await _reclamos.AgregarDocumentoPendienteSiNoExisteAsync(id, "Comprobante de pago de deducible");
            if (analysis.SolicitaMasDocumentos)
                await _reclamos.AgregarDocumentoPendienteSiNoExisteAsync(id, "Documento adicional solicitado por aseguradora");

            if (analysis.RequiereRsa || analysis.RequiereDeducible)
            {
                var actualizado = await _reclamos.GetByIdAsync(id) ?? reclamo;
                var documentos = await _reclamos.GetDocumentosAsync(id);
                var result = await _whatsApp.EnviarSolicitudPagosAprobacionAsync(actualizado, documentos);
                await _auditoria.RegistrarAsync(result.ok ? "AVISO_APROBACION_RECLAMO" : "ERROR_AVISO_APROBACION_RECLAMO", "RECLAMO", id, result.ok ? "Cliente notificado para enviar comprobantes finales." : result.response);
            }
            else if (analysis.SolicitaMasDocumentos)
            {
                var actualizado = await _reclamos.GetByIdAsync(id) ?? reclamo;
                var documentos = await _reclamos.GetDocumentosAsync(id);
                var result = await _whatsApp.EnviarRecordatorioAsync(actualizado, documentos);
                await _auditoria.RegistrarAsync(result.ok ? "AVISO_DOCUMENTO_ADICIONAL_RECLAMO" : "ERROR_DOCUMENTO_ADICIONAL_RECLAMO", "RECLAMO", id, result.ok ? "Cliente notificado para enviar documento adicional." : result.response);
            }
            else if (analysis.AprobadoSinPagosFinales || !analysis.SolicitaMasDocumentos)
            {
                await _reclamos.MarcarTodosDocumentosAsync(id, recibido: true);
                await _reclamos.UpdateEstadoAsync(id, "COMPLETO");
                var actualizado = await _reclamos.GetByIdAsync(id) ?? reclamo;
                var result = await _whatsApp.EnviarAprobacionSinPagosAsync(actualizado);
                await _auditoria.RegistrarAsync(
                    result.ok ? "AVISO_APROBACION_SIN_PAGOS_RECLAMO" : "ERROR_APROBACION_SIN_PAGOS_RECLAMO",
                    "RECLAMO",
                    id,
                    result.ok ? "Cliente notificado que su reclamo fue aprobado sin comprobantes finales." : result.response);
            }
        }

        await _auditoria.RegistrarAsync("RESPUESTA_ASEGURADORA_RECLAMO", "RECLAMO", id, aprobado ? "Aseguradora aprobo expediente; se habilitaron comprobantes finales cuando aplican." : "Respuesta de aseguradora registrada.");
        return NoContent();
    }

    private static bool IsPostApprovalStage(ReclamoWhatsApp reclamo)
    {
        return reclamo.AseguradoraAprobado
            || string.Equals(reclamo.Estado, "ASEGURADORA_APROBADO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reclamo.EstadoReclamo, "ASEGURADORA_APROBADO", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComprobanteFinal(DocumentoDto documento)
    {
        var tipo = InsuranceResponseAnalyzer.NormalizeForMatch(documento.TipoDocumento);
        var nombre = InsuranceResponseAnalyzer.NormalizeForMatch(documento.NombreArchivoOriginal);
        var observacion = InsuranceResponseAnalyzer.NormalizeForMatch(documento.Observacion);
        var combined = $"{tipo} {nombre} {observacion}";
        return combined.Contains("RSA", StringComparison.Ordinal)
            || combined.Contains("RESTITUCION", StringComparison.Ordinal)
            || combined.Contains("RESTITUIR", StringComparison.Ordinal)
            || combined.Contains("DEDUCIBLE", StringComparison.Ordinal)
            || combined.Contains("COASEGURO", StringComparison.Ordinal)
            || combined.Contains("CO ASEGURO", StringComparison.Ordinal)
            || combined.Contains("CO-SEGURO", StringComparison.Ordinal)
            || combined.Contains("COPAGO", StringComparison.Ordinal)
            || combined.Contains("CO PAGO", StringComparison.Ordinal)
            || combined.Contains("CO-PAGO", StringComparison.Ordinal)
            || combined.Contains("COPARTICIPACION", StringComparison.Ordinal)
            || combined.Contains("PARTICIPACION DEL ASEGURADO", StringComparison.Ordinal);
    }

    private static bool IsComprobanteFinalName(string? documento)
    {
        var text = InsuranceResponseAnalyzer.NormalizeForMatch(documento);
        return text.Contains("RSA", StringComparison.Ordinal)
            || text.Contains("DEDUCIBLE", StringComparison.Ordinal)
            || text.Contains("COASEGURO", StringComparison.Ordinal)
            || text.Contains("COPAGO", StringComparison.Ordinal);
    }

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private static string NormalizeSearch(string? value)
    {
        return new string((value ?? "")
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static string NormalizeDocumentText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToUpperInvariant()
            .Replace('Á', 'A')
            .Replace('É', 'E')
            .Replace('Í', 'I')
            .Replace('Ó', 'O')
            .Replace('Ú', 'U')
            .Replace('Ü', 'U')
            .Replace('Ñ', 'N');
    }
}

public sealed record ActualizarDocumentoReclamoRequest(bool Recibido);
public sealed record DocumentoExcepcionRequest(string? Observacion);
public sealed record EnviarAseguradoraRequest(string? CorreoAseguradora, string? CorreoCopia);
public sealed record CorreosAseguradoraRequest(string? Principal, string? Copia);
public sealed record ReclamoDatosBasicosRequest(string? Poliza, string? Reclamo, string? Placa, string? Celular, string? Ciudad);
public sealed record RespuestaAseguradoraRequest(string? Respuesta, bool Aprobado);
public sealed record ReclamoSeguimientoRequest(string EstadoSeguimiento);
