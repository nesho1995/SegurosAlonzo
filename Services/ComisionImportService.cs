using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class ComisionImportService
{
    private readonly ComisionRepository _repo;

    public ComisionImportService(ComisionRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<ComisionDetalle>> PreviewAsync(Stream archivo)
    {
        using var workbook = new XLWorkbook(archivo);
        var ws = workbook.Worksheets.First();
        var map = BuildHeaderMap(ws);
        var rows = new List<ComisionDetalle>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var item = await BuildDetalleAsync(row, map, seen);
            if (!string.IsNullOrWhiteSpace(item.PolizaDetectada) || !string.IsNullOrWhiteSpace(item.ClienteDetectado))
                rows.Add(item);
        }

        return rows;
    }

    public async Task<int> ImportAsync(Stream archivo, string archivoNombre, int? userId)
    {
        var preview = (await PreviewAsync(archivo)).ToList();
        var loteId = await _repo.CreateLoteAsync(preview.FirstOrDefault()?.AseguradoraDetectada, archivoNombre, userId);
        foreach (var item in preview)
        {
            item.LoteId = loteId;
            await _repo.InsertDetalleAsync(item);
        }

        return loteId;
    }

    private async Task<ComisionDetalle> BuildDetalleAsync(IXLRow row, Dictionary<string, int> map, HashSet<string> seen)
    {
        var aseguradora = Get(row, map, "ASEGURADORA");
        var poliza = Get(row, map, "POLIZA");
        var cliente = Get(row, map, "CLIENTE");
        var prima = ParseMoney(Get(row, map, "PRIMA"));
        var porcentaje = ParsePercent(Get(row, map, "PORCENTAJE COMISION"));
        var pagada = ParseMoney(Get(row, map, "COMISION PAGADA"));
        var fechaPago = ParseDate(Get(row, map, "FECHA PAGO"));
        var referencia = Get(row, map, "REFERENCIA");
        var match = await _repo.FindPolicyAsync(aseguradora, poliza, cliente);
        decimal? esperada = null;
        decimal? diferencia = null;
        string estado;
        var obs = "";

        if (match is null)
        {
            estado = "POLIZA_NO_ENCONTRADA";
            obs = "No se encontro una poliza con esos datos.";
        }
        else
        {
            decimal primaBase = prima ?? Convert.ToDecimal(match.PrimaTotal ?? 0);
            esperada = porcentaje.HasValue ? Math.Round(primaBase * porcentaje.Value / 100m, 2) : null;
            diferencia = esperada.HasValue && pagada.HasValue ? Math.Round(pagada.Value - esperada.Value, 2) : null;
            estado = diferencia.HasValue && Math.Abs(diferencia.Value) <= 1m ? "COINCIDE" : "DIFERENCIA_MONTO";
        }

        var key = $"{aseguradora}|{poliza}|{pagada}|{referencia}";
        if (!seen.Add(key))
            estado = "DUPLICADO";

        return new ComisionDetalle
        {
            PolizaId = match?.Id,
            ClienteDetectado = cliente,
            PolizaDetectada = poliza,
            AseguradoraDetectada = aseguradora,
            PrimaDetectada = prima,
            PorcentajeDetectado = porcentaje,
            ComisionDetectada = pagada,
            ComisionEsperada = esperada,
            Diferencia = diferencia,
            FechaPago = fechaPago,
            Referencia = referencia,
            Estado = estado,
            Observaciones = obs
        };
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in ws.Row(1).CellsUsed())
            map[Normalize(cell.GetString())] = cell.Address.ColumnNumber;
        return map;
    }

    private static string Get(IXLRow row, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(Normalize(key), out var col) ? row.Cell(col).GetFormattedString().Trim() : "";
    }

    private static string Normalize(string value)
    {
        var text = (value ?? "").Normalize(NormalizationForm.FormD);
        text = new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(text.ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static decimal? ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Replace("L", "", StringComparison.OrdinalIgnoreCase).Replace("$", "").Replace(",", "").Trim();
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static decimal? ParsePercent(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Replace("%", "").Trim();
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var result) ? result : null;
    }
}
