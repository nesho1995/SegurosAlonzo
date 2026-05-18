using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class ReclamoCorreoProcessingService
{
    private readonly EmailReaderService _emailReader;
    private readonly ReclamoExtractorService _extractor;
    private readonly ReclamoRepository _repo;
    private readonly MessageBuilderService _messageBuilder;
    private readonly ExtractorConfigurableService _extractorConfigurable;
    private readonly AutomationEngineService _automation;
    private readonly AppSettingsRepository _appSettings;
    private readonly CorreoRevisionRepository _correoRevision;
    private readonly RecordatorioRepository _recordatorios;
    private readonly WhatsAppService _whatsApp;
    private readonly AutomaticWhatsAppService _automaticWhatsApp;
    private readonly ILogger<ReclamoCorreoProcessingService> _logger;

    public ReclamoCorreoProcessingService(
        EmailReaderService emailReader,
        ReclamoExtractorService extractor,
        ReclamoRepository repo,
        MessageBuilderService messageBuilder,
        ExtractorConfigurableService extractorConfigurable,
        AutomationEngineService automation,
        AppSettingsRepository appSettings,
        CorreoRevisionRepository correoRevision,
        RecordatorioRepository recordatorios,
        WhatsAppService whatsApp,
        AutomaticWhatsAppService automaticWhatsApp,
        ILogger<ReclamoCorreoProcessingService> logger)
    {
        _emailReader = emailReader;
        _extractor = extractor;
        _repo = repo;
        _messageBuilder = messageBuilder;
        _extractorConfigurable = extractorConfigurable;
        _automation = automation;
        _appSettings = appSettings;
        _correoRevision = correoRevision;
        _recordatorios = recordatorios;
        _whatsApp = whatsApp;
        _automaticWhatsApp = automaticWhatsApp;
        _logger = logger;
    }

    public async Task<ReclamoWorkerEstado> ProcessAsync(int? lookbackHoursOverride = null)
    {
        var estado = new ReclamoWorkerEstado { UltimaEjecucionUtc = DateTime.UtcNow };
        try
        {
            var emails = await _emailReader.GetUnreadEmailsAsync(lookbackHoursOverride);
            estado.CorreosEncontrados = emails.Count;
            var procesados = 0;

            foreach (var email in emails)
            {
                try
                {
                    if (string.Equals(await _correoRevision.GetEstadoAsync(email.MessageId), "ASOCIADO", StringComparison.OrdinalIgnoreCase))
                    {
                        estado.CorreosDuplicados++;
                        estado.Detalles.Add(Detalle(email, "DUPLICADO", "Esta respuesta de aseguradora ya fue asociada anteriormente."));
                        continue;
                    }

                    var esCorreoDeReclamo = await _extractor.EsCorreoDeReclamoAsync(email.Subject);
                    if (!esCorreoDeReclamo)
                    {
                        if (await TryProcessRespuestaAseguradoraAsync(email, estado))
                            continue;

                        estado.CorreosIgnorados++;
                        await RegistrarDetalleAsync(estado, email, "IGNORADO", "El asunto no coincide con el patron de reclamo.");
                        continue;
                    }

                    estado.ReclamosValidos++;

                    if (await _repo.ExistsByMessageIdAsync(email.MessageId))
                    {
                        estado.CorreosDuplicados++;
                        estado.Detalles.Add(Detalle(email, "DUPLICADO", "Ya existe un reclamo creado con este MessageId."));
                        continue;
                    }

                    var reclamo = await _extractor.ExtractAsync(email);
                    await _extractorConfigurable.ProbarAsync(new ExtractorTestRequest
                    {
                        Asunto = email.Subject,
                        Cuerpo = email.Body
                    }, guardarTallerDetectado: true);

                    if (await _repo.ExistsByClaimReferenceAsync(reclamo.Reclamo ?? reclamo.NumeroReclamo, reclamo.Placa))
                    {
                        if (await TryProcessRespuestaAseguradoraAsync(email, estado, requireInsuranceSignals: true))
                            continue;

                        estado.CorreosDuplicados++;
                        await RegistrarDetalleAsync(estado, email, "DUPLICADO", "Ya existe un reclamo con el mismo numero de reclamo y placa.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(reclamo.Conductor))
                        reclamo.Conductor = string.IsNullOrWhiteSpace(reclamo.Asegurado) ? "Cliente" : reclamo.Asegurado;

                    reclamo.MensajeWhatsApp = await _messageBuilder.GenerateMessageAsync(reclamo);
                    reclamo.Estado = "PENDIENTE_ENVIO";
                    var reclamoId = await _repo.InsertAsync(reclamo);
                    await RegistrarDetalleAsync(estado, email, "PROCESADO", $"Reclamo {reclamo.Reclamo ?? reclamo.NumeroReclamo ?? reclamoId.ToString()} creado.", reclamoId);
                    await SafeAutomationAsync("reclamo_nuevo", "RECLAMO", reclamoId, reclamo);
                    await _automaticWhatsApp.ProcesarReclamoNuevoAsync(reclamoId, reclamo);
                    procesados++;
                }
                catch (Exception ex)
                {
                    estado.CorreosConError++;
                    await RegistrarDetalleAsync(estado, email, "ERROR", ex.Message);
                    _logger.LogError(ex, "Error procesando correo {Subject}", email.Subject);
                }
            }

            estado.CorreosProcesados = procesados;
            var envioConfig = await _appSettings.GetEnvioAutomaticoConfigAsync();
            await _recordatorios.GenerarPendientesAsync(envioConfig);
            await _automaticWhatsApp.ProcesarRecordatoriosPendientesAsync();

            var reclamosParaRecordatorio = await _repo.GetParaRecordatorioAsync(
                envioConfig.DiasEntreRecordatoriosReclamo,
                envioConfig.MaxRecordatoriosReclamo);
            foreach (var r in reclamosParaRecordatorio)
            {
                if (!envioConfig.AutoEnviarReclamos)
                    continue;

                var documentos = await _repo.GetDocumentosAsync(r.Id);
                var envio = await _whatsApp.EnviarRecordatorioAsync(r, documentos);
                if (envio.ok)
                    await _repo.MarcarRecordatorioAsync(r.Id);
            }
        }
        catch (Exception ex)
        {
            estado.UltimoError = ex.Message;
            _logger.LogError(ex, "Error general de procesamiento de reclamos por correo.");
        }

        return estado;
    }

    private static CorreoProcesamientoDetalle Detalle(EmailMessageDto email, string estado, string motivo, int? reclamoId = null)
    {
        return new CorreoProcesamientoDetalle
        {
            Subject = email.Subject ?? "",
            MessageId = email.MessageId ?? "",
            Estado = estado,
            Motivo = motivo,
            ReclamoId = reclamoId
        };
    }

    private async Task RegistrarDetalleAsync(ReclamoWorkerEstado estado, EmailMessageDto email, string resultado, string motivo, int? reclamoId = null)
    {
        var detalle = Detalle(email, resultado, motivo, reclamoId);
        estado.Detalles.Add(detalle);
        await _correoRevision.UpsertAsync(detalle, email.Body);
    }

    private async Task<bool> TryProcessRespuestaAseguradoraAsync(EmailMessageDto email, ReclamoWorkerEstado estado, bool requireInsuranceSignals = false)
    {
        var reclamo = await FindReferencedClaimAsync(email);
        if (reclamo is null)
            return false;

        var analysis = InsuranceResponseAnalyzer.Analyze(email);
        if (requireInsuranceSignals && !analysis.TieneSenales)
            return false;

        await _repo.RegistrarRespuestaAseguradoraCorreoAsync(reclamo.Id, BuildInsuranceResponseText(email), analysis.Aprobado);

        if (analysis.RequiereRsa)
            await _repo.AgregarDocumentoPendienteSiNoExisteAsync(reclamo.Id, "Pago de RSA (restitucion de suma asegurada)");

        if (analysis.RequiereCoaseguro)
            await _repo.AgregarDocumentoPendienteSiNoExisteAsync(reclamo.Id, "Pago de coaseguro");

        if (analysis.SolicitaMasDocumentos)
            await _repo.AgregarDocumentoPendienteSiNoExisteAsync(reclamo.Id, "Documento adicional solicitado por aseguradora");

        var acciones = new List<string> { $"Respuesta asociada al reclamo {reclamo.Reclamo ?? reclamo.NumeroReclamo ?? reclamo.Id.ToString()}." };
        if (analysis.Aprobado)
            acciones.Add("Se marco como aprobado por aseguradora.");
        if (analysis.RequiereRsa)
            acciones.Add("Se habilito seguimiento de pago de RSA.");
        if (analysis.RequiereCoaseguro)
            acciones.Add("Se habilito seguimiento de pago de coaseguro.");
        if (analysis.AprobadoSinPagosFinales)
            acciones.Add("Aprobado sin pagos finales pendientes; el cliente puede ingresar el vehiculo.");
        if (analysis.SolicitaMasDocumentos)
            acciones.Add("La aseguradora solicito documento o informacion adicional.");

        if (analysis.RequiereRsa || analysis.RequiereCoaseguro)
        {
            var actualizado = await _repo.GetByIdAsync(reclamo.Id) ?? reclamo;
            var pendientes = await _repo.GetDocumentosAsync(reclamo.Id);
            var envio = await _whatsApp.EnviarSolicitudPagosAprobacionAsync(actualizado, pendientes);
            acciones.Add(envio.ok ? "Cliente notificado por WhatsApp para pagos finales." : $"No se pudo notificar al cliente: {envio.response}");
        }
        else if (analysis.SolicitaMasDocumentos)
        {
            var actualizado = await _repo.GetByIdAsync(reclamo.Id) ?? reclamo;
            var pendientes = await _repo.GetDocumentosAsync(reclamo.Id);
            var envio = await _whatsApp.EnviarRecordatorioAsync(actualizado, pendientes);
            acciones.Add(envio.ok ? "Cliente notificado por WhatsApp." : $"No se pudo notificar al cliente: {envio.response}");
        }
        else if (analysis.AprobadoSinPagosFinales)
        {
            await _repo.MarcarTodosDocumentosAsync(reclamo.Id, recibido: true);
            await _repo.UpdateEstadoAsync(reclamo.Id, "COMPLETO");
        }

        estado.ReclamosValidos++;
        await RegistrarDetalleAsync(estado, email, "ASOCIADO", string.Join(" ", acciones), reclamo.Id);
        return true;
    }

    private async Task<ReclamoWhatsApp?> FindReferencedClaimAsync(EmailMessageDto email)
    {
        var text = InsuranceResponseAnalyzer.NormalizeForMatch($"{email.Subject}\n{email.Body}");
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var reclamos = await _repo.GetAllAsync();
        var byClaimNumber = reclamos
            .Where(r => ContainsReference(text, r.Reclamo) || ContainsReference(text, r.NumeroReclamo))
            .OrderByDescending(r => r.FechaCreacion)
            .FirstOrDefault();
        if (byClaimNumber is not null)
            return byClaimNumber;

        var byPlate = reclamos
            .Where(r => ContainsReference(text, r.Placa))
            .OrderByDescending(r => r.FechaCreacion)
            .Take(2)
            .ToList();

        return byPlate.Count == 1 ? byPlate[0] : null;
    }

    private static bool ContainsReference(string normalizedText, string? value)
    {
        var reference = InsuranceResponseAnalyzer.NormalizeForMatch(value);
        return reference.Length >= 5 && normalizedText.Contains(reference, StringComparison.Ordinal);
    }

    private static InsuranceResponseAnalysis AnalyzeInsuranceResponse(EmailMessageDto email)
    {
        var text = NormalizeForMatch($"{email.Subject}\n{email.Body}");
        var denied = ContainsAny(text, "NO APROBADO", "NO APROBADA", "RECHAZADO", "RECHAZADA", "DECLINADO", "DECLINADA", "NO PROCEDE", "IMPROCEDENTE", "SIN COBERTURA");
        var approved = !denied && ContainsAny(text,
            "APROBADO",
            "APROBADA",
            "APROBACION",
            "APROBACIÓN",
            "ACEPTADO",
            "ACEPTADA",
            "PROCEDENTE",
            "AUTORIZADO",
            "AUTORIZADA",
            "AUTORIZAMOS",
            "SE AUTORIZA",
            "PUEDE INGRESAR",
            "INGRESAR EL VEHICULO",
            "INGRESAR LA UNIDAD",
            "INGRESO DEL VEHICULO",
            "INGRESO DE LA UNIDAD",
            "PROCEDER CON LA REPARACION",
            "PROCEDER CON REPARACION");
        var rsa = ContainsAny(text, "RSA", "RESTITUCION DE SUMA ASEGURADA", "RESTITUCION SUMA ASEGURADA");
        var coaseguro = ContainsAny(text, "COASEGURO", "CO ASEGURO");
        var noFinalPayments = ContainsAny(text,
            "SIN PAGO DE RSA",
            "NO REQUIERE RSA",
            "NO APLICA RSA",
            "SIN RSA",
            "SIN PAGO DE COASEGURO",
            "NO REQUIERE COASEGURO",
            "NO REQUIERE CO ASEGURO",
            "NO APLICA COASEGURO",
            "NO APLICA CO ASEGURO",
            "SIN COASEGURO",
            "SIN CO ASEGURO",
            "SIN PAGAR COASEGURO",
            "SIN PAGAR CO ASEGURO",
            "PUEDE INGRESAR EL CARRO",
            "PUEDE INGRESAR EL VEHICULO",
            "PUEDE INGRESAR LA UNIDAD");
        var moreDocs = ContainsAny(
            text,
            "DOCUMENTO ADICIONAL",
            "DOCUMENTOS ADICIONALES",
            "DOCUMENTACION ADICIONAL",
            "DOCUMENTACION PENDIENTE",
            "FALTA DOCUMENTO",
            "FALTAN DOCUMENTOS",
            "HACE FALTA",
            "NECESITAMOS",
            "SE REQUIERE",
            "REQUERIMOS",
            "PENDIENTE DE RECIBIR",
            "AMPLIAR INFORMACION",
            "INFORMACION ADICIONAL",
            "ENVIAR NUEVAMENTE",
            "CORREGIR");

        var requiresRsa = rsa && !ContainsAny(text, "NO REQUIERE RSA", "NO APLICA RSA", "SIN RSA", "SIN PAGO DE RSA");
        var requiresCoaseguro = coaseguro && !ContainsAny(text, "NO REQUIERE COASEGURO", "NO REQUIERE CO ASEGURO", "NO APLICA COASEGURO", "NO APLICA CO ASEGURO", "SIN COASEGURO", "SIN CO ASEGURO", "SIN PAGO DE COASEGURO", "SIN PAGO DE CO ASEGURO");
        var isApproved = approved || (!denied && (requiresRsa || requiresCoaseguro));
        return new InsuranceResponseAnalysis(isApproved, requiresRsa, requiresCoaseguro, isApproved && !requiresRsa && !requiresCoaseguro && noFinalPayments, !isApproved && moreDocs);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static string BuildInsuranceResponseText(EmailMessageDto email)
    {
        var body = string.IsNullOrWhiteSpace(email.Body) ? "(sin cuerpo)" : email.Body.Trim();
        var text = $"Asunto: {email.Subject}\n\n{body}";
        return text.Length <= 8000 ? text : text[..8000];
    }

    private static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().ToUpperInvariant()
            .Replace('Á', 'A')
            .Replace('É', 'E')
            .Replace('Í', 'I')
            .Replace('Ó', 'O')
            .Replace('Ú', 'U')
            .Replace('Ü', 'U')
            .Replace('Ñ', 'N');

        return normalized;
    }

    private sealed record InsuranceResponseAnalysis(bool Aprobado, bool RequiereRsa, bool RequiereCoaseguro, bool AprobadoSinPagosFinales, bool SolicitaMasDocumentos);

    private async Task SafeAutomationAsync(string evento, string entidadTipo, int entidadId, object data)
    {
        try
        {
            await _automation.EvaluarEventoAsync(evento, entidadTipo, entidadId, data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo ejecutar automatizacion {Evento}.", evento);
        }
    }
}
