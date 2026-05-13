using System.Text.RegularExpressions;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class ExtractorConfigurableService
{
    private readonly AppSettingsRepository _settings;
    private readonly ReclamoExtractorService _fallback;
    private readonly TallerRepository _talleres;

    public ExtractorConfigurableService(
        AppSettingsRepository settings,
        ReclamoExtractorService fallback,
        TallerRepository talleres)
    {
        _settings = settings;
        _fallback = fallback;
        _talleres = talleres;
    }

    public async Task<ExtractorTestResult> ProbarAsync(ExtractorTestRequest request, bool guardarTallerDetectado = false)
    {
        var config = await _settings.GetExtractorAdvancedConfigAsync();
        var baseConfig = await _settings.GetCorreoExtractorConfigAsync();
        var email = new EmailMessageDto
        {
            Subject = request.Asunto,
            Body = request.Cuerpo,
            MessageId = Guid.NewGuid().ToString()
        };

        var reclamo = _fallback.Extract(email);
        var campos = new Dictionary<string, string>
        {
            ["Asegurado"] = reclamo.Asegurado ?? "",
            ["Poliza"] = reclamo.Poliza ?? "",
            ["Placa"] = reclamo.Placa ?? "",
            ["Reclamo"] = reclamo.Reclamo ?? "",
            ["Conductor"] = reclamo.Conductor ?? "",
            ["Celular"] = reclamo.Celular ?? "",
            ["Lugar"] = reclamo.LugarAccidente ?? "",
            ["Ciudad"] = HondurasLocationService.DetectCity(reclamo.LugarAccidente) ?? "",
            ["Fecha"] = reclamo.FechaNotificacion?.ToString("dd/MM/yyyy") ?? "",
            ["Aseguradora"] = DetectarAseguradora(request, config),
            ["Taller"] = DetectarTaller(request.Cuerpo)
        };

        var obligatorios = Split(config.CamposObligatorios);
        var faltantes = obligatorios
            .Where(campo => !campos.TryGetValue(campo, out var value) || string.IsNullOrWhiteSpace(value))
            .ToList();

        var confianza = obligatorios.Count == 0
            ? 100
            : Math.Max(0, (int)Math.Round(((obligatorios.Count - faltantes.Count) / (double)obligatorios.Count) * 100));

        var sugeridos = await _talleres.SugerirAsync(string.IsNullOrWhiteSpace(campos["Ciudad"]) ? campos["Lugar"] : campos["Ciudad"], campos["Aseguradora"], "AUTO");
        var detectado = await CrearDetectadoSiAplicaAsync(campos, request, guardarTallerDetectado);

        return new ExtractorTestResult
        {
            TextoOriginal = $"{request.Asunto}\n\n{request.Cuerpo}".Trim(),
            CamposDetectados = campos,
            CamposFaltantes = faltantes,
            Confianza = confianza,
            Mensaje = ConstruirMensaje(config.PlantillaWhatsApp, campos),
            TalleresSugeridos = sugeridos.ToList(),
            TallerDetectado = detectado
        };
    }

    public async Task<bool> EsCorreoPermitidoAsync(string? remitente, string? asunto)
    {
        var config = await _settings.GetExtractorAdvancedConfigAsync();
        var remitentes = Split(config.RemitentesPermitidos);
        var palabras = Split(config.PalabrasClaveAsunto);

        var remitenteOk = remitentes.Count == 0 ||
            remitentes.Any(x => (remitente ?? "").Contains(x, StringComparison.OrdinalIgnoreCase));

        var asuntoOk = palabras.Count == 0 ||
            palabras.Any(x => (asunto ?? "").Contains(x, StringComparison.OrdinalIgnoreCase));

        return remitenteOk && asuntoOk;
    }

    private async Task<TallerDetectado?> CrearDetectadoSiAplicaAsync(
        Dictionary<string, string> campos,
        ExtractorTestRequest request,
        bool guardar)
    {
        var nombre = campos.GetValueOrDefault("Taller") ?? "";

        if (string.IsNullOrWhiteSpace(nombre))
            return null;

        var sugeridos = await _talleres.SugerirAsync(
            string.IsNullOrWhiteSpace(campos.GetValueOrDefault("Ciudad")) ? campos.GetValueOrDefault("Lugar") : campos.GetValueOrDefault("Ciudad"),
            campos.GetValueOrDefault("Aseguradora"),
            "AUTO");
        if (sugeridos.Any(x => x.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
            return null;

        var item = new TallerDetectado
        {
            Nombre = nombre,
            Ciudad = string.IsNullOrWhiteSpace(campos.GetValueOrDefault("Ciudad")) ? campos.GetValueOrDefault("Lugar") : campos.GetValueOrDefault("Ciudad"),
            Aseguradora = campos.GetValueOrDefault("Aseguradora"),
            TextoOrigen = request.Cuerpo,
            Estado = "PENDIENTE"
        };

        if (guardar)
            item.Id = await _talleres.InsertDetectadoAsync(item);

        return item;
    }

    private static string DetectarAseguradora(ExtractorTestRequest request, ExtractorAdvancedConfig config)
    {
        var texto = $"{request.Remitente}\n{request.Asunto}\n{request.Cuerpo}";
        foreach (var regla in SplitLines(config.AseguradorasReglas))
        {
            var parts = regla.Split("=>", 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && texto.Contains(parts[0], StringComparison.OrdinalIgnoreCase))
                return parts[1];
        }

        var match = Regex.Match(texto, @"Aseguradora\s*:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string DetectarTaller(string? cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
            return "";

        var match = Regex.Match(cuerpo, @"Taller\s*:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string ConstruirMensaje(string plantilla, Dictionary<string, string> campos)
    {
        var mensaje = plantilla ?? "";
        foreach (var campo in campos)
            mensaje = mensaje.Replace("{" + campo.Key + "}", campo.Value ?? "", StringComparison.OrdinalIgnoreCase);

        return mensaje.Trim();
    }

    private static List<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value
            .Replace(";", ",")
            .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> SplitLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Enumerable.Empty<string>();

        return value
            .Replace("\r\n", "\n")
            .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
