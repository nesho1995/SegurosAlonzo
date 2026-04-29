using System.Globalization;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class ReclamoExtractorService
{
    private readonly AppSettingsRepository _settings;

    public ReclamoExtractorService(AppSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<ReclamoWhatsApp> ExtractAsync(EmailMessageDto email)
    {
        var config = await _settings.GetCorreoExtractorConfigAsync();
        return Extract(email, config);
    }

    public ReclamoWhatsApp Extract(EmailMessageDto email)
    {
        return Extract(email, new CorreoExtractorConfig());
    }

    public async Task<bool> EsCorreoDeReclamoAsync(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return false;

        var config = await _settings.GetCorreoExtractorConfigAsync();
        var (placa, reclamo) = ExtraerDatosAsunto(subject, config.SubjectPattern);

        return !string.IsNullOrWhiteSpace(placa) && !string.IsNullOrWhiteSpace(reclamo);
    }

    private static ReclamoWhatsApp Extract(EmailMessageDto email, CorreoExtractorConfig config)
    {
        var body = LimpiarTexto(email.Body);
        var subject = email.Subject ?? "";

        var aseguradora = FirstNonEmpty(ExtraerCampo(body, "Aseguradora"), ExtraerLinea(body, @"Aseguradora:\s*(.+)"));
        var asegurado = FirstNonEmpty(ExtraerLinea(body, config.AseguradoPattern), ExtraerCampo(body, "Asegurado"));
        var poliza = FirstNonEmpty(ExtraerLinea(body, config.PolizaPattern), ExtraerCampo(body, "Poliza"), ExtraerCampo(body, "Póliza"));
        var caracteristicas = FirstNonEmpty(ExtraerLinea(body, config.CaracteristicasPattern), ExtraerCampo(body, "Vehiculo"), ExtraerCampo(body, "Vehículo"));
        var conductor = FirstNonEmpty(ExtraerLinea(body, config.ConductorPattern), ExtraerCampo(body, "Conductor"));
        var celular = FirstNonEmpty(ExtraerLinea(body, config.CelularPattern), ExtraerCampo(body, "Celular"));

        var fechaTexto = FirstNonEmpty(ExtraerPrimero(body, config.FechaPattern), ExtraerCampo(body, "Fecha"));
        var lugar = FirstNonEmpty(ExtraerPrimero(body, config.LugarPattern), ExtraerCampo(body, "Lugar"));
        var (placa, reclamo) = ExtraerDatosAsunto(subject, config.SubjectPattern);

        if (string.IsNullOrWhiteSpace(placa))
            placa = ExtraerPlaca(caracteristicas);

        if (string.IsNullOrWhiteSpace(placa))
            placa = ExtraerPlaca(body);

        if (string.IsNullOrWhiteSpace(reclamo))
            reclamo = ExtraerReclamo(subject);

        return new ReclamoWhatsApp
        {
            MessageId = string.IsNullOrWhiteSpace(email.MessageId)
                ? Guid.NewGuid().ToString()
                : email.MessageId,

            Asunto = subject,
            Aseguradora = aseguradora,
            Asegurado = asegurado,
            Poliza = poliza,
            Placa = placa,
            Reclamo = reclamo,
            Conductor = conductor,
            Celular = NormalizarCelular(celular),
            FechaNotificacion = ParseFecha(fechaTexto),
            LugarAccidente = lugar,
            Estado = "PENDIENTE"
        };
    }

    private static string LimpiarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        return texto
            .Replace("\u00A0", " ")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }

    private static string ExtraerLinea(string texto, string patron)
    {
        var match = Regex.Match(
            texto,
            patron,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string ExtraerPrimero(string texto, string patron)
    {
        var match = Regex.Match(
            texto,
            patron,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string ExtraerCampo(string texto, string campo)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(campo))
            return "";

        var escaped = Regex.Escape(campo);
        var match = Regex.Match(
            texto,
            $@"^\s*{escaped}\s*:\s*(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
    }

    private static (string placa, string reclamo) ExtraerDatosAsunto(string texto, string patron)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return ("", "");

        var match = Regex.Match(
            texto.Trim(),
            patron,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return ("", "");

        var placa = match.Groups["placa"]?.Value;
        var reclamo = match.Groups["reclamo"]?.Value;

        if (string.IsNullOrWhiteSpace(placa) && match.Groups.Count > 1)
            placa = match.Groups[1].Value;

        if (string.IsNullOrWhiteSpace(reclamo) && match.Groups.Count > 2)
            reclamo = match.Groups[2].Value;

        return (placa?.ToUpper().Trim() ?? "", reclamo?.ToUpper().Trim() ?? "");
    }

    private static string ExtraerPlaca(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var match = Regex.Match(
            texto,
            @"\b[A-Z]{2,4}-?[0-9]{3,4}\b",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Value.ToUpper().Trim() : "";
    }

    private static string ExtraerReclamo(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var match = Regex.Match(
            texto,
            @"\b[A-Z]{2,5}-[0-9]{2,5}-[0-9]{4}\b",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Value.ToUpper().Trim() : "";
    }

    private static string NormalizarCelular(string celular)
    {
        if (string.IsNullOrWhiteSpace(celular))
            return "";

        var limpio = celular
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("+", "")
            .Trim();

        if (limpio.Length == 8)
            limpio = "504" + limpio;

        return limpio;
    }

    private static DateTime? ParseFecha(string fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
            return null;

        var formatos = new[]
        {
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy"
        };

        return DateTime.TryParseExact(
            fecha.Trim(),
            formatos,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result)
                ? result
                : null;
    }
}
