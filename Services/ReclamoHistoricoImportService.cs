using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class ReclamoHistoricoImportService
{
    private readonly ReclamoRepository _reclamos;

    private static readonly Dictionary<string, string> DocumentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FORMULARIORECLAMO"] = "Aviso de accidente",
        ["AVISOACCIDENTE"] = "Aviso de accidente",
        ["AVISODEACCIDENTEOFORMULARIORECLAMO"] = "Aviso de accidente",
        ["CERTTRANSITO"] = "Certificacion de transito",
        ["CERTIFICACIONTRANSITO"] = "Certificacion de transito",
        ["CERTIFICADOTRANSITO"] = "Certificacion de transito",
        ["LICENCIA"] = "Licencia del conductor",
        ["IDENTIDAD"] = "Tarjeta de identidad del conductor",
        ["BOLETAREVISION"] = "Boleta de circulacion",
        ["BOLETADECIRCULACION"] = "Boleta de circulacion",
        ["BOLETADECIRCULACIONOREVISION"] = "Boleta de circulacion",
        ["COTIZACIONES"] = "2 cotizaciones de talleres",
        ["PAGODEDUCIBLE"] = "Comprobante de pago de deducible",
        ["COMPROBANTEDEDEDUCIBLE"] = "Comprobante de pago de deducible",
        ["PAGORSA"] = "Comprobante de pago de RSA",
        ["COMPROBANTEDERSA"] = "Comprobante de pago de RSA"
    };

    public ReclamoHistoricoImportService(ReclamoRepository reclamos)
    {
        _reclamos = reclamos;
    }

    public async Task<ReclamoHistoricoImportPreview> PreviewAsync(Stream archivo)
    {
        using var workbook = new XLWorkbook(archivo);
        var ws = workbook.Worksheets.First();
        var headers = BuildHeaderMap(ws);
        var preview = new ReclamoHistoricoImportPreview();

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var item = await BuildRowAsync(row, headers);
            if (HasAnyClaimData(item))
                preview.Rows.Add(item);
        }

        return preview;
    }

    public async Task<ReclamoHistoricoImportResult> ImportAsync(Stream archivo)
    {
        var preview = await PreviewAsync(archivo);
        var result = new ReclamoHistoricoImportResult();

        foreach (var row in preview.Rows)
        {
            if (row.Errors.Count > 0)
            {
                result.Rechazados++;
                continue;
            }

            if (row.Duplicado)
            {
                result.Duplicados++;
                continue;
            }

            var reclamo = new ReclamoWhatsApp
            {
                MessageId = $"historico:{row.Reclamo}:{row.Poliza}:{row.RowNumber}:{Guid.NewGuid():N}",
                Asunto = $"Reclamo historico {row.Reclamo}".Trim(),
                Aseguradora = "CREFISA",
                Asegurado = row.Cliente,
                Poliza = row.Poliza,
                Placa = row.Placa,
                Reclamo = row.Reclamo,
                NumeroReclamo = row.Reclamo,
                Conductor = row.Conductor,
                Celular = NormalizePhone(row.Celular),
                FechaNotificacion = row.FechaNotificacion,
                LugarAccidente = string.IsNullOrWhiteSpace(row.Ciudad) ? "" : row.Ciudad,
                Descripcion = BuildDescription(row),
                TipoReclamo = "AUTOS",
                Estado = "EN_SEGUIMIENTO",
                EstadoReclamo = "EN_SEGUIMIENTO",
                CiudadDetectada = string.IsNullOrWhiteSpace(row.Ciudad) ? null : row.Ciudad,
                ActualizadoEn = DateTime.UtcNow
            };

            var id = await _reclamos.InsertAsync(reclamo);
            await AplicarDocumentosAsync(id, row);
            result.Importados++;
        }

        return result;
    }

    private async Task AplicarDocumentosAsync(int reclamoId, ReclamoHistoricoImportRow row)
    {
        var documentosRecibidos = row.DocumentosRecibidos;
        foreach (var final in DetectarComprobantesFinales(row))
        {
            await _reclamos.AgregarDocumentoPendienteSiNoExisteAsync(reclamoId, final.Documento);
            documentosRecibidos[final.Documento] = final.Recibido;
        }

        var documentos = (await _reclamos.GetDocumentosAsync(reclamoId)).ToList();
        foreach (var item in documentos)
        {
            var key = NormalizeKey(item.Documento);
            var recibido = documentosRecibidos
                .Where(x => NormalizeKey(x.Key) == key)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (recibido)
                await _reclamos.ActualizarDocumentoAsync(item.Id, reclamoId, true);
        }
    }

    private async Task<ReclamoHistoricoImportRow> BuildRowAsync(IXLRow row, Dictionary<string, int> headers)
    {
        var item = new ReclamoHistoricoImportRow
        {
            RowNumber = row.RowNumber(),
            Conductor = Get(row, headers, "CONDUCTOR"),
            Cliente = Get(row, headers, "CLIENTE"),
            Poliza = NormalizePolicy(Get(row, headers, "POLIZA")),
            Reclamo = Get(row, headers, "RECLAMO").ToUpperInvariant(),
            Vehiculo = Get(row, headers, "VEHICULO"),
            Ciudad = Get(row, headers, "CIUDAD"),
            Celular = Get(row, headers, "CELULAR"),
            Observaciones = Get(row, headers, "OBSERVACIONES"),
            FechaNotificacion = ParseDate(Get(row, headers, "FECHANOTIFICACION"))
        };
        item.Placa = ExtractPlate(item.Vehiculo);

        if (string.IsNullOrWhiteSpace(item.Conductor))
            item.Errors.Add(new ImportIssue("CONDUCTOR", "El conductor es requerido."));
        if (string.IsNullOrWhiteSpace(item.Poliza))
        {
            if (!string.IsNullOrWhiteSpace(item.Reclamo) && !string.IsNullOrWhiteSpace(item.Placa))
                item.Warnings.Add(new ImportIssue("POLIZA", "Sin poliza; se importara para seguimiento y debe completarse despues."));
            else
                item.Errors.Add(new ImportIssue("POLIZA", "La poliza es requerida cuando no hay suficiente referencia de reclamo/placa."));
        }
        if (string.IsNullOrWhiteSpace(item.Reclamo))
            item.Warnings.Add(new ImportIssue("RECLAMO", "Sin numero de reclamo; se importara para seguimiento por poliza/conductor."));

        foreach (var doc in DocumentMap)
        {
            var raw = Get(row, headers, doc.Key);
            var recibido = IsReceived(raw);
            if (item.DocumentosRecibidos.TryGetValue(doc.Value, out var yaRecibido))
                item.DocumentosRecibidos[doc.Value] = yaRecibido || recibido;
            else
                item.DocumentosRecibidos[doc.Value] = recibido;
        }

        foreach (var final in DetectarComprobantesFinales(item))
        {
            var estado = final.Recibido ? "recibido" : "pendiente";
            item.Warnings.Add(new ImportIssue("OBSERVACIONES", $"{final.Documento} detectado como {estado}."));
        }

        if (!string.IsNullOrWhiteSpace(item.Reclamo))
            item.Duplicado = await _reclamos.ExistsByClaimReferenceAsync(item.Reclamo, item.Placa);

        return item;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var key = NormalizeKey(cell.GetString());
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = cell.Address.ColumnNumber;
        }

        return result;
    }

    private static string Get(IXLRow row, Dictionary<string, int> headers, string key)
    {
        return headers.TryGetValue(NormalizeKey(key), out var col)
            ? row.Cell(col).GetFormattedString().Trim()
            : "";
    }

    private static bool HasAnyClaimData(ReclamoHistoricoImportRow row)
    {
        return !string.IsNullOrWhiteSpace(row.Conductor)
            || !string.IsNullOrWhiteSpace(row.Cliente)
            || !string.IsNullOrWhiteSpace(row.Poliza)
            || !string.IsNullOrWhiteSpace(row.Reclamo)
            || !string.IsNullOrWhiteSpace(row.Vehiculo);
    }

    private static bool IsReceived(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = NormalizeKey(value);
        return text is not ("NO" or "N" or "PENDIENTE" or "FALTA" or "FALTANTE" or "0");
    }

    private static List<(string Documento, bool Recibido)> DetectarComprobantesFinales(ReclamoHistoricoImportRow row)
    {
        var result = new List<(string Documento, bool Recibido)>();
        var obs = NormalizeKey(row.Observaciones);
        var mentionsDeducible = ContainsAny(obs, "DEDUCIBLE", "COASEGURO", "COPAGO");
        var mentionsRsa = ContainsAny(obs, "RSA", "RESTITUCION");
        var paid = ContainsAny(obs, "YAPAGO", "PAGADO", "CANCELADO", "REALIZOELPAGO", "HIZOELPAGO", "PAGODEDEDUCIBLEYRSA", "PAGOELDEDUCIBLE", "PAGOLARSA");
        var pending = ContainsAny(obs, "COBRO", "COBRANDO", "PENDIENTE", "SALDO", "FALTA", "HACEFALTA");

        if (row.DocumentosRecibidos.TryGetValue("Comprobante de pago de deducible", out var deducibleRecibido) && deducibleRecibido)
            AddIfNeeded("Comprobante de pago de deducible", !(mentionsDeducible && pending));
        if (row.DocumentosRecibidos.TryGetValue("Comprobante de pago de RSA", out var rsaRecibido) && rsaRecibido)
            AddIfNeeded("Comprobante de pago de RSA", !(mentionsRsa && pending));

        if (string.IsNullOrWhiteSpace(obs))
            return result;

        var recibido = paid && !pending;

        if (mentionsDeducible)
            AddIfNeeded("Comprobante de pago de deducible", recibido);
        if (mentionsRsa)
            AddIfNeeded("Comprobante de pago de RSA", recibido);

        return result;

        void AddIfNeeded(string documento, bool recibido)
        {
            var index = result.FindIndex(x => string.Equals(x.Documento, documento, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                result.Add((documento, recibido));
            else
                result[index] = (documento, result[index].Recibido || recibido);
        }
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(text.Contains);
    }

    private static string NormalizePolicy(string value)
    {
        var text = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return Regex.Replace(text.ToUpperInvariant(), @"\s+", "");
    }

    private static string ExtractPlate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var match = Regex.Match(value, @"\b[A-Z]{2,4}-?[0-9]{3,4}\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : "";
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var serial)
            && serial > 20000
            && serial < 90000)
        {
            return DateTime.FromOADate(serial).Date;
        }

        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" };
        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;
    }

    private static string BuildDescription(ReclamoHistoricoImportRow row)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.Vehiculo))
            parts.Add($"Vehiculo: {row.Vehiculo}");
        if (!string.IsNullOrWhiteSpace(row.Ciudad))
            parts.Add($"Ciudad: {row.Ciudad}");
        if (!string.IsNullOrWhiteSpace(row.Observaciones))
            parts.Add($"Observaciones: {row.Observaciones}");
        return string.Join(Environment.NewLine, parts);
    }

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var first = Regex.Split(value, @"[/,;]")
            .Select(x => Regex.Replace(x, @"\D", ""))
            .FirstOrDefault(x => x.Length >= 8) ?? "";
        if (first.Length > 8)
            first = first[^8..];

        return first.Length == 8 ? "504" + first : first;
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }
}
