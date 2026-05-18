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

    public async Task<bool> EsCorreoDeReclamoAsync(EmailMessageDto email)
    {
        if (email is null || (string.IsNullOrWhiteSpace(email.Subject) && string.IsNullOrWhiteSpace(email.Body)))
            return false;

        var config = await _settings.GetCorreoExtractorConfigAsync();
        var subject = email.Subject ?? "";
        var body = LimpiarTexto(email.Body);
        var (placaAsunto, reclamoAsunto) = ExtraerDatosAsunto(subject, config.SubjectPattern);
        var placa = FirstNonEmpty(ExtraerPlacaDesdeCuerpo(body), placaAsunto);
        var reclamo = FirstNonEmpty(ExtraerReclamoDesdeCuerpo(body), reclamoAsunto);

        return !string.IsNullOrWhiteSpace(placa) && !string.IsNullOrWhiteSpace(reclamo);
    }

    private static ReclamoWhatsApp Extract(EmailMessageDto email, CorreoExtractorConfig config)
    {
        var body = LimpiarTexto(email.Body);
        var subject = email.Subject ?? "";

        var aseguradora = FirstNonEmpty(ExtraerCampo(body, "Aseguradora", "Compania", "Compañia", "Compañía", "Empresa aseguradora"), ExtraerLinea(body, @"Aseguradora:\s*(.+)"));
        var asegurado = FirstNonEmpty(ExtraerLinea(body, config.AseguradoPattern), ExtraerCampo(body, "Asegurado", "Nombre asegurado", "Cliente", "Contratante"));
        var poliza = FirstNonEmpty(ExtraerLinea(body, config.PolizaPattern), ExtraerCampo(body, "Poliza", "Póliza", "No. Poliza", "No Poliza", "Numero de poliza", "Número de póliza"));
        var caracteristicas = FirstNonEmpty(ExtraerLinea(body, config.CaracteristicasPattern), ExtraerCampo(body, "Vehiculo", "Vehículo", "Unidad", "Bien asegurado", "Caracteristicas del bien asegurado", "Características del bien asegurado"));
        var conductor = FirstNonEmpty(ExtraerLinea(body, config.ConductorPattern), ExtraerCampo(body, "Conductor", "Motorista", "Piloto", "Chofer"));
        var celular = FirstNonEmpty(ExtraerLinea(body, config.CelularPattern), ExtraerCampo(body, "Celular", "Telefono", "Teléfono", "Movil", "Móvil", "WhatsApp"));

        var fechaTexto = FirstNonEmpty(
            ExtraerPrimero(body, config.FechaPattern),
            ExtraerCampo(body, "Fecha", "Fecha de notificacion", "Fecha notificacion", "Fecha de aviso", "Fecha del accidente", "Fecha de accidente", "Fecha del siniestro", "Fecha de siniestro"));
        var lugar = ExtraerLugar(body, config);
        var (placaAsunto, reclamoAsunto) = ExtraerDatosAsunto(subject, config.SubjectPattern);
        var placa = FirstNonEmpty(ExtraerPlacaDesdeCuerpo(body), placaAsunto);
        var reclamo = FirstNonEmpty(ExtraerReclamoDesdeCuerpo(body), reclamoAsunto);

        if (string.IsNullOrWhiteSpace(placa))
            placa = FirstNonEmpty(ExtraerCampo(body, "Placa", "No. Placa", "Numero de placa", "Número de placa", "Matricula", "Matrícula"), ExtraerPlaca(caracteristicas));

        if (string.IsNullOrWhiteSpace(placa))
            placa = ExtraerPlaca(body);

        if (string.IsNullOrWhiteSpace(reclamo))
            reclamo = FirstNonEmpty(ExtraerCampo(body, "Reclamo", "No. Reclamo", "No Reclamo", "Numero de reclamo", "Número de reclamo", "Referencia"), ExtraerReclamo(subject));

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

        return match.Success ? LimpiarValorCampo(match.Groups[1].Value) : "";
    }

    private static string ExtraerPrimero(string texto, string patron)
    {
        var match = Regex.Match(
            texto,
            patron,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? LimpiarValorCampo(match.Groups[1].Value) : "";
    }

    private static string ExtraerCampo(string texto, params string[] campos)
    {
        if (string.IsNullOrWhiteSpace(texto) || campos.Length == 0)
            return "";

        var escaped = string.Join("|", campos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Regex.Escape));
        if (string.IsNullOrWhiteSpace(escaped))
            return "";

        var match = Regex.Match(
            texto,
            $@"^\s*(?:{escaped})\s*:\s*(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return match.Success ? LimpiarValorCampo(match.Groups[1].Value) : "";
    }

    private static string ExtraerLugar(string body, CorreoExtractorConfig config)
    {
        var lugar = FirstNonEmpty(
            ExtraerCampo(
                body,
                "Lugar",
                "Lugar del accidente",
                "Lugar de accidente",
                "Lugar del siniestro",
                "Lugar de siniestro",
                "Ocurrido en",
                "Ocurrio en",
                "Ocurrió en",
                "Accidente ocurrido en",
                "Siniestro ocurrido en",
                "Direccion del accidente",
                "Dirección del accidente",
                "Ubicacion",
                "Ubicación",
                "Ciudad",
                "Municipio",
                "Localidad"),
            ExtraerPrimero(body, config.LugarPattern));

        lugar = LimpiarValorCampo(lugar);
        var ciudad = HondurasLocationService.DetectCity(lugar);

        if (EsLugarDemasiadoCorto(lugar) && !string.IsNullOrWhiteSpace(ciudad))
            return ciudad;

        if (string.IsNullOrWhiteSpace(lugar))
            return FirstNonEmpty(ExtraerCampo(body, "Ciudad", "Municipio", "Localidad"), ciudad);

        return lugar;
    }

    private static string LimpiarValorCampo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var cleaned = Regex.Replace(value, @"\s+", " ").Trim();
        cleaned = Regex.Split(
            cleaned,
            @"\s+(?:Aseguradora|Asegurado|Cliente|Conductor|Motorista|Piloto|Chofer|Celular|Telefono|Tel[eé]fono|P[oó]liza|Veh[ií]culo|Placa|Fecha|Reclamo|Ciudad|Municipio|Lugar)\s*:",
            RegexOptions.IgnoreCase)[0].Trim();

        return cleaned.Trim(' ', '.', ',', ';', ':', '-', '*');
    }

    private static bool EsLugarDemasiadoCorto(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim();
        return normalized.Length < 8
            || Regex.IsMatch(normalized, @"^(?:barrio|bo\.?|colonia|col\.?|residencial|aldea|caserio|caser[ií]o|sector)\s+(?:el|la|los|las)?$", RegexOptions.IgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
    }

    private static string ExtraerPlacaDesdeCuerpo(string body)
    {
        var value = ExtraerCampo(body, "Placa", "No. Placa", "Numero de placa", "NÃºmero de placa", "Matricula", "MatrÃ­cula");
        return string.IsNullOrWhiteSpace(value) ? "" : ExtraerPlaca(value);
    }

    private static string ExtraerReclamoDesdeCuerpo(string body)
    {
        var value = ExtraerCampo(body, "Reclamo", "No. Reclamo", "No Reclamo", "Numero de reclamo", "NÃºmero de reclamo", "Referencia", "Caso", "Expediente");
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var structured = ExtraerReclamo(value);
        return string.IsNullOrWhiteSpace(structured)
            ? LimpiarValorCampo(value).ToUpperInvariant().Trim()
            : structured;
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
            return (ExtraerPlaca(texto), ExtraerReclamo(texto));

        var placa = match.Groups["placa"]?.Value;
        var reclamo = match.Groups["reclamo"]?.Value;

        if (string.IsNullOrWhiteSpace(placa) && match.Groups.Count > 1)
            placa = match.Groups[1].Value;

        if (string.IsNullOrWhiteSpace(reclamo) && match.Groups.Count > 2)
            reclamo = match.Groups[2].Value;

        placa = LimpiarValorCampo(placa).ToUpper().Trim();
        reclamo = LimpiarValorCampo(reclamo).ToUpper().Trim();

        if (string.IsNullOrWhiteSpace(placa))
            placa = ExtraerPlaca(texto);

        if (string.IsNullOrWhiteSpace(reclamo))
            reclamo = ExtraerReclamo(texto);

        return (placa, reclamo);
    }

    private static string ExtraerPlaca(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var sinReclamo = Regex.Replace(
            texto,
            @"\b[A-Z]{2,5}-[0-9]{2,5}-[0-9]{3,4}\b",
            " ",
            RegexOptions.IgnoreCase);

        var match = Regex.Match(
            sinReclamo,
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
            @"\b[A-Z]{2,5}-[0-9]{2,5}-[0-9]{3,4}\b",
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
