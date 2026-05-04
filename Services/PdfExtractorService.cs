using System.Text;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ReclamosWhatsApp.Services;

/// <summary>
/// Extrae campos de cotización de seguros a partir del texto de un PDF.
/// </summary>
public static class PdfExtractorService
{
    // ─── Extracción de texto plano ──────────────────────────────────────────

    public static string ExtractText(Stream pdfStream)
    {
        using var doc = PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            foreach (var word in page.GetWords())
                sb.Append(word.Text).Append(' ');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ─── Parsear campos desde texto ─────────────────────────────────────────

    public static ComparativoItem ParseFields(string text, string fileName)
    {
        var item = new ComparativoItem
        {
            NombreArchivo = fileName,
            TextoExtraido = text,
        };

        var t = text;

        item.Aseguradora    = ExtractAseguradora(t, fileName);
        item.PrimaAnual     = TryAmount(t, PrimaAnualPatterns);
        item.PrimaMensual   = TryAmount(t, PrimaMensualPatterns);
        item.SumaAsegurada  = TryAmount(t, SumaAseguradaPatterns);
        item.VigenciaDesde  = TryDate(t, VigenciaDesdePatterns);
        item.VigenciaHasta  = TryDate(t, VigenciaHastaPatterns);
        item.FormaPago      = ExtractFormaPago(t);

        (item.DeducibleColision, item.DeducibleColisionEsPorcentaje) = TryDeducible(t, DeducibleColisionPatterns);
        (item.DeducibleRobo,     item.DeducibleRoboEsPorcentaje)     = TryDeducible(t, DeducibleRoboPatterns);

        (item.DescuentoContado,      item.DescuentoEsPorcentaje)    = TryDeducible(t, DescuentoContadoPatterns);
        (item.RecargoFinanciamiento, item.RecargoEsPorcentaje)      = TryDeducible(t, RecargoPatterns);

        // Calcular prima contado / financiada
        item.PrimaContado    = CalculatePrimaContado(item);
        item.PrimaFinanciada = CalculatePrimaFinanciada(item);

        item.CoberturasJson  = ExtractCoberturas(t);
        item.ExclusionesJson = ExtractExclusiones(t);

        return item;
    }

    // ─── Aseguradora ────────────────────────────────────────────────────────

    static readonly string[] KnownInsurers =
    [
        "ATLÁNTIDA", "ATLANTIDA", "FICOHSA", "CREFISA", "PALIC", "INTERAMERICANA",
        "MAPFRE", "LAFISE", "BANPAÍS", "BANPAIS", "QUALITAS", "QUALITAS",
        "AIG", "HSBC", "SEGUROS DEL PAÍS", "SEGUROS DEL PAIS", "CONTINENTAL",
        "OPTIMUM", "SURAMERICANA", "ASSA", "PRUDENTIAL", "METROPOLITAN",
        "NACIONAL", "COLONIAL", "PREMIER", "AMERICAN LIFE", "ALLIANZ",
        "ZURICH", "GENERALI", "FIDES", "BANHCAFE", "BANRURAL",
        "CREDOMATIC", "BAC", "DAVIVIENDA", "CITI", "SCOTIABANK",
    ];

    static string ExtractAseguradora(string text, string fileName)
    {
        var upper = text.ToUpperInvariant();
        foreach (var k in KnownInsurers)
            if (upper.Contains(k.ToUpperInvariant()))
                return ToTitleCase(k);

        // Intenta detectar "SEGUROS XXXX" o "XXXX SEGUROS"
        var m = Regex.Match(text,
            @"(?:SEGUROS|ASEGURADORA)\s+([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ\s]{2,30})",
            RegexOptions.IgnoreCase);
        if (m.Success) return ToTitleCase(m.Groups[1].Value.Trim());

        m = Regex.Match(text,
            @"([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ\s]{2,30})\s+(?:SEGUROS|ASEGURADORA)",
            RegexOptions.IgnoreCase);
        if (m.Success) return ToTitleCase(m.Groups[1].Value.Trim());

        // Fallback: nombre sin extensión
        return Path.GetFileNameWithoutExtension(fileName);
    }

    // ─── Patrones de campos ─────────────────────────────────────────────────

    static readonly string[] PrimaAnualPatterns =
    [
        @"prima\s*(?:neta\s*)?(?:total|anual)\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"prima\s+anual\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"total\s+prima\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"prima\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"[HL]\.\s*([\d,\.]{5,})",
    ];

    static readonly string[] PrimaMensualPatterns =
    [
        @"prima\s*mensual\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"cuota\s*mensual\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"pago\s*mensual\s*[:\s]+[HL]?\s*([\d,\.]+)",
    ];

    static readonly string[] SumaAseguradaPatterns =
    [
        @"suma\s*asegurada\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"valor\s*asegurado\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"valor\s*del\s*veh[íi]culo\s*[:\s]+[HL]?\s*([\d,\.]+)",
        @"valor\s*comercial\s*[:\s]+[HL]?\s*([\d,\.]+)",
    ];

    static readonly string[] VigenciaDesdePatterns =
    [
        @"(?:vigencia|inicio|desde)[^\d]*(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4})",
        @"(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{4})\s*(?:al|a|hasta|\-)",
    ];

    static readonly string[] VigenciaHastaPatterns =
    [
        @"(?:vencimiento|hasta|fin|expira)[^\d]*(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4})",
        @"(?:al|hasta|\-)\s*(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{4})",
    ];

    static readonly string[] DeducibleColisionPatterns =
    [
        @"deducible\s*(?:por\s*)?(?:colisi[oó]n|da[nñ]os\s*propios)[^\d]*([\d,\.]+)\s*(%)?",
        @"(?:colisi[oó]n|choque)[^\d]*([\d,\.]+)\s*(%)?",
    ];

    static readonly string[] DeducibleRoboPatterns =
    [
        @"deducible\s*(?:por\s*)?robo\s*(?:total)?[^\d]*([\d,\.]+)\s*(%)?",
        @"robo\s*total[^\d]*([\d,\.]+)\s*(%)?",
    ];

    static readonly string[] DescuentoContadoPatterns =
    [
        @"descuento\s*(?:por\s*)?(?:pronto\s*pago|contado|pago\s*[úu]nico)[^\d]*([\d,\.]+)\s*(%)?",
        @"dto\.?\s*contado[^\d]*([\d,\.]+)\s*(%)?",
        @"descuento[^\d]*([\d,\.]+)\s*(%)",   // solo si tiene % explícito
    ];

    static readonly string[] RecargoPatterns =
    [
        @"recargo\s*(?:por\s*)?financiamiento[^\d]*([\d,\.]+)\s*(%)?",
        @"recargo\s*financiero[^\d]*([\d,\.]+)\s*(%)?",
        @"inter[eé]s\s*(?:por\s*)?financiamiento[^\d]*([\d,\.]+)\s*(%)?",
        @"costo\s*financiero[^\d]*([\d,\.]+)\s*(%)?",
    ];

    // ─── Helpers ────────────────────────────────────────────────────────────

    static decimal? TryAmount(string text, string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!m.Success) continue;
            var raw = m.Groups[1].Value.Replace(",", "").Replace(" ", "");
            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var val)
                && val > 0)
                return Math.Round(val, 2);
        }
        return null;
    }

    static (decimal? value, bool isPct) TryDeducible(string text, string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!m.Success) continue;
            var raw = m.Groups[1].Value.Replace(",", "").Replace(" ", "");
            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out var val))
                continue;
            bool isPct = m.Groups.Count > 2 && m.Groups[2].Value == "%";
            // valores ≤ 100 sin símbolo probablemente son porcentaje
            if (!isPct && val <= 100) isPct = true;
            return (Math.Round(val, 2), isPct);
        }
        return (null, false);
    }

    static string? TryDate(string text, string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return null;
    }

    static string ExtractFormaPago(string text)
    {
        if (Regex.IsMatch(text, @"\bcontado\b", RegexOptions.IgnoreCase))      return "Contado";
        if (Regex.IsMatch(text, @"\bmensual\b", RegexOptions.IgnoreCase))       return "Mensual";
        if (Regex.IsMatch(text, @"\btrimestral\b", RegexOptions.IgnoreCase))    return "Trimestral";
        if (Regex.IsMatch(text, @"\bsemestral\b", RegexOptions.IgnoreCase))     return "Semestral";
        if (Regex.IsMatch(text, @"\banual\b", RegexOptions.IgnoreCase))         return "Anual";
        var mCuotas = Regex.Match(text, @"(\d+)\s*cuotas?", RegexOptions.IgnoreCase);
        if (mCuotas.Success) return $"{mCuotas.Groups[1].Value} cuotas";
        return "No especificado";
    }

    static readonly string[] CoberturaKeywords =
    [
        "Robo total", "Robo parcial", "Daños propios", "Colisión", "Choque",
        "Responsabilidad civil", "Daños a terceros", "Gastos médicos",
        "Asistencia en carretera", "Vehículo de reemplazo", "Cristales",
        "Desastres naturales", "Inundación", "Terremoto", "Incendio",
        "Vandalismo", "Accidente", "Muerte accidental", "Invalidez",
        "Grúa", "Remolque", "Rotura de llanta",
    ];

    static string ExtractCoberturas(string text)
    {
        var found = new List<string>();
        foreach (var kw in CoberturaKeywords)
        {
            // Busca la keyword seguida de: ✓, Si, Incluido, Cubierto, o sin negación cerca
            var pattern = $@"(?<!\bno\s)(?<!\bexclu[iy])\b{Regex.Escape(kw)}\b";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                found.Add(kw);
        }
        return System.Text.Json.JsonSerializer.Serialize(found);
    }

    static string ExtractExclusiones(string text)
    {
        var found = new List<string>();
        // Busca sección de exclusiones
        var m = Regex.Match(text,
            @"(?:exclusiones?|no\s+cubre?|no\s+incluye?)[:\s]+([\s\S]{0,800}?)(?:\n\n|\z)",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var block = m.Groups[1].Value;
            var lines = block.Split(['\n', '\r', '•', '-', '*'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                var clean = l.Trim();
                if (clean.Length > 5 && clean.Length < 120)
                    found.Add(clean);
                if (found.Count >= 8) break;
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(found);
    }

    static decimal? CalculatePrimaContado(ComparativoItem item)
    {
        if (item.PrimaAnual == null) return null;
        if (item.DescuentoContado == null) return item.PrimaAnual;
        return item.DescuentoEsPorcentaje
            ? Math.Round(item.PrimaAnual.Value * (1 - item.DescuentoContado.Value / 100), 2)
            : Math.Round(item.PrimaAnual.Value - item.DescuentoContado.Value, 2);
    }

    static decimal? CalculatePrimaFinanciada(ComparativoItem item)
    {
        if (item.PrimaAnual == null) return null;
        if (item.RecargoFinanciamiento == null) return item.PrimaAnual;
        return item.RecargoEsPorcentaje
            ? Math.Round(item.PrimaAnual.Value * (1 + item.RecargoFinanciamiento.Value / 100), 2)
            : Math.Round(item.PrimaAnual.Value + item.RecargoFinanciamiento.Value, 2);
    }

    static string ToTitleCase(string s)
        => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
}
