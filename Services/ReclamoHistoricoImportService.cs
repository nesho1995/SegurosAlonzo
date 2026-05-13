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
        ["CERTTRANSITO"] = "Certificacion de transito",
        ["CERTIFICACIONTRANSITO"] = "Certificacion de transito",
        ["LICENCIA"] = "Licencia del conductor",
        ["BOLETAREVISION"] = "Boleta de circulacion",
        ["BOLETADECIRCULACION"] = "Boleta de circulacion",
        ["COTIZACIONES"] = "2 cotizaciones de talleres"
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
                FechaNotificacion = row.FechaNotificacion,
                LugarAccidente = "",
                Descripcion = row.Vehiculo,
                TipoReclamo = "AUTOS",
                Estado = "EN_SEGUIMIENTO",
                EstadoReclamo = "EN_SEGUIMIENTO",
                ActualizadoEn = DateTime.UtcNow
            };

            var id = await _reclamos.InsertAsync(reclamo);
            await AplicarDocumentosAsync(id, row.DocumentosRecibidos);
            result.Importados++;
        }

        return result;
    }

    private async Task AplicarDocumentosAsync(int reclamoId, Dictionary<string, bool> documentosRecibidos)
    {
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
            FechaNotificacion = ParseDate(Get(row, headers, "FECHANOTIFICACION"))
        };
        item.Placa = ExtractPlate(item.Vehiculo);

        if (string.IsNullOrWhiteSpace(item.Conductor))
            item.Errors.Add(new ImportIssue("CONDUCTOR", "El conductor es requerido."));
        if (string.IsNullOrWhiteSpace(item.Poliza))
            item.Errors.Add(new ImportIssue("POLIZA", "La poliza es requerida."));
        if (string.IsNullOrWhiteSpace(item.Reclamo))
            item.Warnings.Add(new ImportIssue("RECLAMO", "Sin numero de reclamo; se importara para seguimiento por poliza/conductor."));

        foreach (var doc in DocumentMap)
        {
            var raw = Get(row, headers, doc.Key);
            item.DocumentosRecibidos[doc.Value] = IsReceived(raw);
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

        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" };
        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;
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
