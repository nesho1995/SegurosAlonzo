using System.Text;
using System.Text.RegularExpressions;
using PDFtoImage;
using ReclamosWhatsApp.Models;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;

namespace ReclamosWhatsApp.Services;

/// <summary>
/// Extrae texto de un PDF: primero intenta extracción directa (PdfPig),
/// si el texto es insuficiente cae a OCR con Tesseract sobre imágenes renderizadas
/// con PDFtoImage.
/// </summary>
public static class PdfExtractorService
{
    // Umbral: si el texto directo tiene menos de este nro de caracteres, usamos OCR
    private const int MinTextLength = 80;

    // ─── Extracción de texto ─────────────────────────────────────────────────

    public static string ExtractText(Stream pdfStream)
    {
        // Leemos el stream completo en memoria para poder rebobinar
        byte[] pdfBytes;
        using (var ms = new MemoryStream())
        {
            pdfStream.CopyTo(ms);
            pdfBytes = ms.ToArray();
        }

        // 1) Intento directo con PdfPig
        var direct = ExtractDirect(pdfBytes);
        if (direct.Length >= MinTextLength)
            return direct;

        // 2) Fallback: OCR con Tesseract
        return ExtractOcr(pdfBytes);
    }

    static string ExtractDirect(byte[] pdfBytes)
    {
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
            {
                foreach (var word in page.GetWords())
                    sb.Append(word.Text).Append(' ');
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
        catch { return ""; }
    }

    static string ExtractOcr(byte[] pdfBytes)
    {
        var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!Directory.Exists(tessDataPath))
            return "";

        var sb = new StringBuilder();
        try
        {
            using var engine = new TesseractEngine(tessDataPath, "spa+eng", EngineMode.Default);

            // Renderiza cada página del PDF como imagen
            using var pdfStream = new MemoryStream(pdfBytes);
            var pages = Conversion.ToImages(pdfStream, options: new RenderOptions(Dpi: 200));

            foreach (var skBitmap in pages)
            {
                using var bitmap = skBitmap;
                // Convierte SKBitmap → PNG bytes → Pix de Tesseract
                using var skData = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                var imgBytes = skData.ToArray();

                using var pix = Pix.LoadFromMemory(imgBytes);
                using var ocrPage = engine.Process(pix);
                sb.AppendLine(ocrPage.GetText());
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[OCR error: {ex.Message}]");
        }

        return sb.ToString().Trim();
    }

    // ─── Parsear campos desde texto ──────────────────────────────────────────

    public static ComparativoItem ParseFields(string text, string fileName)
    {
        var item = new ComparativoItem
        {
            NombreArchivo = fileName,
            TextoExtraido = text,
        };

        item.Aseguradora    = ExtractAseguradora(text, fileName);
        item.PrimaAnual     = TryAmount(text, PrimaAnualPatterns);
        item.PrimaMensual   = TryAmount(text, PrimaMensualPatterns);
        item.SumaAsegurada  = TryAmount(text, SumaAseguradaPatterns);
        item.VigenciaDesde  = TryDate(text, VigenciaDesdePatterns);
        item.VigenciaHasta  = TryDate(text, VigenciaHastaPatterns);
        item.FormaPago      = ExtractFormaPago(text);

        (item.DeducibleColision, item.DeducibleColisionEsPorcentaje) = TryDeducible(text, DeducibleColisionPatterns);
        (item.DeducibleRobo,     item.DeducibleRoboEsPorcentaje)     = TryDeducible(text, DeducibleRoboPatterns);
        (item.DescuentoContado,  item.DescuentoEsPorcentaje)         = TryDeducible(text, DescuentoContadoPatterns);
        (item.RecargoFinanciamiento, item.RecargoEsPorcentaje)       = TryDeducible(text, RecargoPatterns);

        item.PrimaContado    = CalcPrimaContado(item);
        item.PrimaFinanciada = CalcPrimaFinanciada(item);

        item.CoberturasJson  = ExtractCoberturas(text);
        item.ExclusionesJson = ExtractExclusiones(text);

        return item;
    }

    // ─── Aseguradora ─────────────────────────────────────────────────────────

    static readonly string[] KnownInsurers =
    [
        "ATLÁNTIDA", "ATLANTIDA", "FICOHSA", "CREFISA", "PALIC", "INTERAMERICANA",
        "MAPFRE", "LAFISE", "BANPAÍS", "BANPAIS", "QUALITAS", "SEGUROS NACIONAL",
        "NACIONAL", "AIG", "CONTINENTAL", "OPTIMUM", "SURAMERICANA", "ASSA",
        "PRUDENTIAL", "METROPOLITAN", "COLONIAL", "PREMIER", "AMERICAN LIFE",
        "ALLIANZ", "ZURICH", "GENERALI", "FIDES", "BANHCAFE", "BANRURAL",
        "BAC", "DAVIVIENDA", "SCOTIABANK",
    ];

    static string ExtractAseguradora(string text, string fileName)
    {
        var upper = text.ToUpperInvariant();
        foreach (var k in KnownInsurers)
            if (upper.Contains(k.ToUpperInvariant()))
                return ToTitleCase(k);

        var m = Regex.Match(text,
            @"(?:SEGUROS|ASEGURADORA)\s+([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ\s]{2,30})",
            RegexOptions.IgnoreCase);
        if (m.Success) return ToTitleCase(m.Groups[1].Value.Trim());

        m = Regex.Match(text,
            @"([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ\s]{2,30})\s+(?:SEGUROS|ASEGURADORA)",
            RegexOptions.IgnoreCase);
        if (m.Success) return ToTitleCase(m.Groups[1].Value.Trim());

        return Path.GetFileNameWithoutExtension(fileName);
    }

    // ─── Patrones ────────────────────────────────────────────────────────────

    static readonly string[] PrimaAnualPatterns =
    [
        @"prima\s*(?:neta\s*)?(?:total|anual)\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"prima\s+anual\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"total\s+prima\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"prima\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"[LH]\.\s*([\d,\.]{5,})",
    ];

    static readonly string[] PrimaMensualPatterns =
    [
        @"prima\s*mensual\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"cuota\s*mensual\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"pago\s*mensual\s*[:\s]+[LH]?\s*([\d,\.]+)",
    ];

    static readonly string[] SumaAseguradaPatterns =
    [
        @"suma\s*asegurada\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"valor\s*asegurado\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"valor\s*del\s*veh[íi]culo\s*[:\s]+[LH]?\s*([\d,\.]+)",
        @"valor\s*comercial\s*[:\s]+[LH]?\s*([\d,\.]+)",
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
        @"descuento[^\d]*([\d,\.]+)\s*(%)",
    ];

    static readonly string[] RecargoPatterns =
    [
        @"recargo\s*(?:por\s*)?financiamiento[^\d]*([\d,\.]+)\s*(%)?",
        @"recargo\s*financiero[^\d]*([\d,\.]+)\s*(%)?",
        @"inter[eé]s\s*(?:por\s*)?financiamiento[^\d]*([\d,\.]+)\s*(%)?",
        @"costo\s*financiero[^\d]*([\d,\.]+)\s*(%)?",
    ];

    // ─── Helpers ─────────────────────────────────────────────────────────────

    static decimal? TryAmount(string text, string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!m.Success) continue;
            var raw = m.Groups[1].Value.Replace(",", "").Replace(" ", "");
            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
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
                System.Globalization.CultureInfo.InvariantCulture, out var val)) continue;
            bool isPct = m.Groups.Count > 2 && m.Groups[2].Value == "%";
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
        if (Regex.IsMatch(text, @"\bcontado\b",    RegexOptions.IgnoreCase)) return "Contado";
        if (Regex.IsMatch(text, @"\bmensual\b",    RegexOptions.IgnoreCase)) return "Mensual";
        if (Regex.IsMatch(text, @"\btrimestral\b", RegexOptions.IgnoreCase)) return "Trimestral";
        if (Regex.IsMatch(text, @"\bsemestral\b",  RegexOptions.IgnoreCase)) return "Semestral";
        if (Regex.IsMatch(text, @"\banual\b",      RegexOptions.IgnoreCase)) return "Anual";
        var mc = Regex.Match(text, @"(\d+)\s*cuotas?", RegexOptions.IgnoreCase);
        if (mc.Success) return $"{mc.Groups[1].Value} cuotas";
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
            var pattern = $@"(?<!\bno\s)(?<!\bexclu[iy])\b{Regex.Escape(kw)}\b";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                found.Add(kw);
        }
        return System.Text.Json.JsonSerializer.Serialize(found);
    }

    static string ExtractExclusiones(string text)
    {
        var found = new List<string>();
        var m = Regex.Match(text,
            @"(?:exclusiones?|no\s+cubre?|no\s+incluye?)[:\s]+([\s\S]{0,800}?)(?:\n\n|\z)",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var block = m.Groups[1].Value;
            foreach (var l in block.Split(['\n', '\r', '•', '-', '*'], StringSplitOptions.RemoveEmptyEntries))
            {
                var clean = l.Trim();
                if (clean.Length > 5 && clean.Length < 120) found.Add(clean);
                if (found.Count >= 8) break;
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(found);
    }

    static decimal? CalcPrimaContado(ComparativoItem item)
    {
        if (item.PrimaAnual == null) return null;
        if (item.DescuentoContado == null) return item.PrimaAnual;
        return item.DescuentoEsPorcentaje
            ? Math.Round(item.PrimaAnual.Value * (1 - item.DescuentoContado.Value / 100), 2)
            : Math.Round(item.PrimaAnual.Value - item.DescuentoContado.Value, 2);
    }

    static decimal? CalcPrimaFinanciada(ComparativoItem item)
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
