using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services.DataQuality;
using System.Security.Cryptography;
using System.Text;

namespace ReclamosWhatsApp.Services;

public class AutomaticWhatsAppService
{
    private readonly AppSettingsRepository _settings;
    private readonly ReclamoRepository _reclamos;
    private readonly RecordatorioRepository _recordatorios;
    private readonly NotificacionRepository _notificaciones;
    private readonly WhatsAppEnvioLogRepository _enviosLog;
    private readonly WhatsAppService _whatsApp;
    private readonly PhoneNormalizationService _phones;
    private readonly AuditoriaService _auditoria;
    private readonly ILogger<AutomaticWhatsAppService> _logger;

    public AutomaticWhatsAppService(
        AppSettingsRepository settings,
        ReclamoRepository reclamos,
        RecordatorioRepository recordatorios,
        NotificacionRepository notificaciones,
        WhatsAppEnvioLogRepository enviosLog,
        WhatsAppService whatsApp,
        PhoneNormalizationService phones,
        AuditoriaService auditoria,
        ILogger<AutomaticWhatsAppService> logger)
    {
        _settings = settings;
        _reclamos = reclamos;
        _recordatorios = recordatorios;
        _notificaciones = notificaciones;
        _enviosLog = enviosLog;
        _whatsApp = whatsApp;
        _phones = phones;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task ProcesarReclamoNuevoAsync(int reclamoId, ReclamoWhatsApp reclamo)
    {
        await _notificaciones.CrearAsync(
            "NUEVO_RECLAMO",
            "Nuevo reclamo",
            $"Reclamo nuevo de {reclamo.Conductor ?? reclamo.Asegurado ?? "cliente sin nombre"}.",
            "RECLAMO",
            reclamoId,
            $"reclamo-nuevo:{reclamoId}");

        var config = await _settings.GetEnvioAutomaticoConfigAsync();
        if (!config.AutoEnviarReclamos)
        {
            await _enviosLog.RegistrarAsync($"reclamo:{reclamoId}:preparado", "RECLAMO", reclamoId, reclamo.Celular, "RECLAMO_NUEVO", true, "PREPARADO", reclamo.MensajeWhatsApp, "Pendiente para revision manual.");
            await _auditoria.RegistrarAsync("RECORDATORIO_PENDIENTE", "RECLAMO", reclamoId, "Reclamo dejado pendiente por automatizacion desactivada.");
            return;
        }

        var autoReference = $"reclamo:{reclamoId}:auto";
        if (IsAlreadyHandled(await _enviosLog.GetEstadoAsync(autoReference)))
            return;

        var phone = NormalizePhone(reclamo.Celular);
        var mensajeHash = HashMessage(reclamo.MensajeWhatsApp);
        if (string.IsNullOrWhiteSpace(phone))
        {
            const string error = "Cliente sin telefono valido para WhatsApp.";
            await _reclamos.UpdateEstadoAsync(reclamoId, "ERROR", error);
            await NotifyPhoneErrorAsync("RECLAMO", reclamoId, $"reclamo-sin-telefono:{reclamoId}", error);
            await _enviosLog.RegistrarAsync($"reclamo:{reclamoId}:auto", "RECLAMO", reclamoId, reclamo.Celular, "RECLAMO_NUEVO", true, "ERROR", reclamo.MensajeWhatsApp, error);
            await _auditoria.RegistrarAsync("ERROR_WHATSAPP", "RECLAMO", reclamoId, error);
            return;
        }

        if (await _enviosLog.ExistsSuccessfulDuplicateAsync("RECLAMO", reclamoId, "RECLAMO_NUEVO", phone, mensajeHash, DateTime.UtcNow))
        {
            await _enviosLog.RegistrarAsync($"reclamo:{reclamoId}:auto:duplicado", "RECLAMO", reclamoId, phone, "RECLAMO_NUEVO", true, "DUPLICADO", reclamo.MensajeWhatsApp, "Envio automatico evitado por duplicado.");
            await _notificaciones.CrearAsync("DUPLICADO_EVITADO", "Duplicado evitado", "No se envio WhatsApp porque ya existia un envio exitoso equivalente.", "RECLAMO", reclamoId, $"wa-dup-reclamo:{reclamoId}");
            await _auditoria.RegistrarAsync("ENVIO_DUPLICADO_EVITADO", "RECLAMO", reclamoId, "Envio automatico evitado por regla anti-duplicados.");
            return;
        }

        var result = await _whatsApp.SendTemplateAsync(reclamo);
        await _reclamos.UpdateEstadoAsync(reclamoId, result.ok ? "ENVIADO" : "ERROR", result.ok ? null : result.response);
        await _enviosLog.RegistrarAsync(autoReference, "RECLAMO", reclamoId, phone, "RECLAMO_NUEVO", true, result.ok ? "ENVIADO" : "ERROR", reclamo.MensajeWhatsApp, result.response);
        await _auditoria.RegistrarAsync(result.ok ? "ENVIAR_WHATSAPP_AUTO" : "ERROR_WHATSAPP", "RECLAMO", reclamoId, result.ok ? "WhatsApp automatico enviado para reclamo." : "Fallo el WhatsApp automatico del reclamo.");

        if (result.ok)
        {
            await _notificaciones.CrearAsync("WHATSAPP_AUTO_ENVIADO", "WhatsApp enviado", "Se envio WhatsApp automatico para un reclamo.", "RECLAMO", reclamoId, $"wa-auto-ok-reclamo:{reclamoId}");
        }
        else
        {
            await NotifyWhatsAppFailureAsync("RECLAMO", reclamoId, $"wa-auto-error-reclamo:{reclamoId}", result.response);
        }
    }

    public async Task ProcesarRecordatoriosPendientesAsync()
    {
        var config = await _settings.GetEnvioAutomaticoConfigAsync();
        var pendientes = await _recordatorios.GetPendientesParaAutoEnvioAsync();

        foreach (var recordatorio in pendientes)
        {
            await _notificaciones.CrearAsync(
                "RECORDATORIO_GENERADO",
                "Recordatorio generado",
                $"{recordatorio.Cliente}: {recordatorio.Asunto}",
                "RECORDATORIO",
                recordatorio.Id,
                $"recordatorio-generado:{recordatorio.Id}");

            if (!IsAutoEnabled(recordatorio, config))
            {
                await _auditoria.RegistrarAsync("RECORDATORIO_PENDIENTE", "RECORDATORIO", recordatorio.Id, "Recordatorio dejado pendiente por automatizacion desactivada.");
                continue;
            }

            if (IsAlreadyHandled(await _enviosLog.GetEstadoAsync($"recordatorio:{recordatorio.Id}:auto")))
                continue;

            await EnviarRecordatorioAsync(recordatorio, automatico: true);
        }
    }

    public async Task<(bool ok, string response)> EnviarRecordatorioAsync(Recordatorio recordatorio, bool automatico)
    {
        if (string.Equals(recordatorio.Estado, "ENVIADO", StringComparison.OrdinalIgnoreCase))
            return (false, "El recordatorio ya fue enviado.");

        var phone = NormalizePhone(recordatorio.Telefono);
        var reference = $"recordatorio:{recordatorio.Id}:{(automatico ? "auto" : "manual")}";
        var mensajeHash = HashMessage(recordatorio.Mensaje);
        if (string.IsNullOrWhiteSpace(phone))
        {
            const string error = "Cliente sin telefono valido para WhatsApp.";
            await _recordatorios.MarcarEnvioAsync(recordatorio.Id, false, error);
            await NotifyPhoneErrorAsync("RECORDATORIO", recordatorio.Id, $"recordatorio-sin-telefono:{recordatorio.Id}", error);
            await _enviosLog.RegistrarAsync(reference, "RECORDATORIO", recordatorio.Id, recordatorio.Telefono, recordatorio.Tipo, automatico, "ERROR", recordatorio.Mensaje, error);
            await _auditoria.RegistrarAsync("ERROR_RECORDATORIO", "RECORDATORIO", recordatorio.Id, error);
            return (false, error);
        }

        if (await _enviosLog.ExistsSuccessfulDuplicateAsync("RECORDATORIO", recordatorio.Id, recordatorio.Tipo, phone, mensajeHash, recordatorio.FechaObjetivo))
        {
            await _enviosLog.RegistrarAsync($"{reference}:duplicado", "RECORDATORIO", recordatorio.Id, phone, recordatorio.Tipo, automatico, "DUPLICADO", recordatorio.Mensaje, "Envio evitado por duplicado.");
            await _notificaciones.CrearAsync("DUPLICADO_EVITADO", "Duplicado evitado", $"{recordatorio.Cliente}: se evito envio duplicado de recordatorio.", "RECORDATORIO", recordatorio.Id, $"wa-dup-recordatorio:{recordatorio.Id}:{(automatico ? "auto" : "manual")}");
            await _auditoria.RegistrarAsync("ENVIO_DUPLICADO_EVITADO", "RECORDATORIO", recordatorio.Id, "Envio de recordatorio evitado por regla anti-duplicados.");
            return (false, "Envio duplicado evitado.");
        }

        var result = await _whatsApp.SendConfiguredMessageAsync(phone, recordatorio.Mensaje);
        await _recordatorios.MarcarEnvioAsync(recordatorio.Id, result.ok, result.response);
        await _enviosLog.RegistrarAsync(reference, "RECORDATORIO", recordatorio.Id, phone, recordatorio.Tipo, automatico, result.ok ? "ENVIADO" : "ERROR", recordatorio.Mensaje, result.response);
        await _auditoria.RegistrarAsync(result.ok ? (automatico ? "ENVIAR_RECORDATORIO_AUTO" : "ENVIAR_RECORDATORIO") : "ERROR_RECORDATORIO", "RECORDATORIO", recordatorio.Id, result.ok ? "Recordatorio enviado por WhatsApp." : "No se pudo enviar el recordatorio.");

        if (result.ok)
        {
            await _notificaciones.CrearAsync("WHATSAPP_AUTO_ENVIADO", automatico ? "WhatsApp automatico enviado" : "WhatsApp enviado", $"{recordatorio.Cliente}: {recordatorio.Asunto}", "RECORDATORIO", recordatorio.Id, $"wa-ok-recordatorio:{recordatorio.Id}:{(automatico ? "auto" : "manual")}");
        }
        else
        {
            await NotifyWhatsAppFailureAsync("RECORDATORIO", recordatorio.Id, $"wa-error-recordatorio:{recordatorio.Id}:{(automatico ? "auto" : "manual")}", result.response);
        }

        return result;
    }

    private string NormalizePhone(string? value)
    {
        var normalized = _phones.NormalizeMany(value);
        return normalized.WhatsappReady ? normalized.PrincipalWhatsApp : "";
    }

    private static bool IsAutoEnabled(Recordatorio recordatorio, EnvioAutomaticoConfig config)
    {
        return recordatorio.Tipo.ToUpperInvariant() switch
        {
            "PAGO" => config.AutoEnviarRecordatoriosPago,
            "RENOVACION" or "VENCIMIENTO" => config.AutoEnviarRecordatoriosPoliza,
            _ => false
        };
    }

    private static bool IsAlreadyHandled(string? estado)
    {
        return string.Equals(estado, "ENVIADO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(estado, "DUPLICADO", StringComparison.OrdinalIgnoreCase);
    }

    private async Task NotifyPhoneErrorAsync(string entidadTipo, int entidadId, string referencia, string message)
    {
        await _notificaciones.CrearAsync("CLIENTE_SIN_TELEFONO_VALIDO", "Cliente sin telefono valido", message, entidadTipo, entidadId, referencia);
    }

    private async Task NotifyWhatsAppFailureAsync(string entidadTipo, int entidadId, string referencia, string response)
    {
        _logger.LogWarning("Fallo WhatsApp para {EntidadTipo} {EntidadId}: {Response}", entidadTipo, entidadId, response);
        await _notificaciones.CrearAsync("WHATSAPP_FALLIDO", "WhatsApp fallido", "No se pudo enviar WhatsApp. Puedes reintentarlo manualmente desde la bandeja.", entidadTipo, entidadId, referencia);
    }

    private static string HashMessage(string? message)
    {
        var payload = Encoding.UTF8.GetBytes(message ?? "");
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
