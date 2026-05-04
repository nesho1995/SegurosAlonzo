using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.CotizacionesVer)]
[Route("api/comparativos")]
public class ComparativoApiController : ControllerBase
{
    private readonly ComparativoRepository _repo;
    private readonly EmpresaConfiguracionRepository _empresa;

    public ComparativoApiController(
        ComparativoRepository repo,
        EmpresaConfiguracionRepository empresa)
    {
        _repo    = repo;
        _empresa = empresa;
    }

    private int UserId => int.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", out var id) ? id : 0;

    // ─── Lista ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null)
    {
        var result = await _repo.GetAsync(page, pageSize, q);
        return Ok(result);
    }

    // ─── Detalle ─────────────────────────────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        var d = await _repo.GetDetalleAsync(id);
        if (d == null) return NotFound(new { error = "No encontrado." });
        return Ok(d);
    }

    // ─── Crear ───────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ComparativoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cliente))
            return BadRequest(new { error = "El nombre del cliente es requerido." });
        var id = await _repo.CrearAsync(UserId, req.Cliente, req.Vehiculo, req.Notas);
        return Ok(new { id });
    }

    // ─── Actualizar ──────────────────────────────────────────────────────────

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ComparativoRequest req)
    {
        await _repo.ActualizarAsync(id, req.Cliente, req.Vehiculo, req.Notas, req.Estado);
        return Ok(new { ok = true });
    }

    // ─── Eliminar ────────────────────────────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _repo.EliminarAsync(id);
        return Ok(new { ok = true });
    }

    // ─── Subir PDF ───────────────────────────────────────────────────────────

    [HttpPost("{id:int}/pdf")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> SubirPdf(int id, IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { error = "No se recibió ningún archivo." });
        if (!archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Solo se aceptan archivos PDF." });

        var detalle = await _repo.GetDetalleAsync(id);
        if (detalle == null) return NotFound(new { error = "Comparativo no encontrado." });

        string texto;
        try
        {
            using var stream = archivo.OpenReadStream();
            texto = PdfExtractorService.ExtractText(stream);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"No se pudo leer el PDF: {ex.Message}" });
        }

        var item = PdfExtractorService.ParseFields(texto, archivo.FileName);
        item.ComparativoId = id;

        var itemId = await _repo.AgregarItemAsync(item);
        await _repo.RecalcularRankingAsync(id);

        var actualizado = await _repo.GetDetalleAsync(id);
        var creado = actualizado!.Items.First(i => i.Id == itemId);
        return Ok(creado);
    }

    // ─── Actualizar item ─────────────────────────────────────────────────────

    [HttpPut("{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> ActualizarItem(int id, int itemId, [FromBody] ComparativoItem req)
    {
        req.Id             = itemId;
        req.ComparativoId  = id;
        await _repo.ActualizarItemAsync(req);
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ─── Eliminar item ────────────────────────────────────────────────────────

    [HttpDelete("{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> EliminarItem(int id, int itemId)
    {
        await _repo.EliminarItemAsync(itemId);
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ─── Export Excel ─────────────────────────────────────────────────────────

    [HttpGet("{id:int}/excel")]
    public async Task<IActionResult> ExportExcel(int id)
    {
        var d = await _repo.GetDetalleAsync(id);
        if (d == null) return NotFound();

        var items = d.Items;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Comparativo");

        ws.Cell(1, 1).Value = $"Comparativo — {d.Comparativo.Cliente}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        if (items.Count > 0) ws.Range(1, 1, 1, items.Count + 1).Merge();

        ws.Cell(2, 1).Value = "Campo";
        ws.Cell(2, 1).Style.Font.Bold = true;
        for (var col = 0; col < items.Count; col++)
        {
            var cell = ws.Cell(2, col + 2);
            cell.Value = items[col].Aseguradora;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1d4ed8");
            cell.Style.Font.FontColor = XLColor.White;
        }

        (string Label, Func<ComparativoItemDetalle, string> Fn)[] rowDefs =
        [
            ("Posición",              i => i.Posicion.HasValue ? $"#{i.Posicion}" : "—"),
            ("Score",                 i => i.Score.HasValue    ? $"{i.Score:F1}/100" : "—"),
            ("Prima anual",           i => Lps(i.PrimaAnual)),
            ("Prima contado",         i => Lps(i.PrimaContado)),
            ("Descuento contado",     i => Pct(i.DescuentoContado, i.DescuentoEsPorcentaje)),
            ("Prima financiada",      i => Lps(i.PrimaFinanciada)),
            ("Recargo financ.",       i => Pct(i.RecargoFinanciamiento, i.RecargoEsPorcentaje)),
            ("Ahorro al contado",     i => Lps(i.AhorroContado)),
            ("Prima mensual",         i => Lps(i.PrimaMensual)),
            ("Suma asegurada",        i => Lps(i.SumaAsegurada)),
            ("Deducible colisión",    i => Pct(i.DeducibleColision, i.DeducibleColisionEsPorcentaje)),
            ("Deducible robo total",  i => Pct(i.DeducibleRobo, i.DeducibleRoboEsPorcentaje)),
            ("Forma de pago",         i => i.FormaPago ?? "—"),
            ("Vigencia",              i => $"{i.VigenciaDesde ?? "—"} → {i.VigenciaHasta ?? "—"}"),
            ("Coberturas",            i => string.Join(", ", i.Coberturas)),
            ("Exclusiones",           i => string.Join(", ", i.Exclusiones)),
        ];

        for (var r = 0; r < rowDefs.Length; r++)
        {
            var rowN = r + 3;
            ws.Cell(rowN, 1).Value = rowDefs[r].Label;
            ws.Cell(rowN, 1).Style.Font.Bold = true;
            if (r % 2 == 0) ws.Row(rowN).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            for (var col = 0; col < items.Count; col++)
                ws.Cell(rowN, col + 2).Value = rowDefs[r].Fn(items[col]);
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"comparativo_{d.Comparativo.Cliente}.xlsx");
    }

    // ─── Export PDF ──────────────────────────────────────────────────────────

    [HttpGet("{id:int}/pdf-reporte")]
    public async Task<IActionResult> ExportPdf(int id)
    {
        var d = await _repo.GetDetalleAsync(id);
        if (d == null) return NotFound();

        var empresa = await _empresa.GetAsync();
        var items   = d.Items;
        var comp    = d.Comparativo;

        var bestPrimaId = items.Where(i => (i.PrimaContado ?? i.PrimaAnual) > 0)
            .MinBy(i => i.PrimaContado ?? i.PrimaAnual ?? decimal.MaxValue)?.Id;
        var bestScoreId = items.Count > 0 ? items.MaxBy(i => i.Score)?.Id : null;

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(empresa.NombreEmpresa).Bold().FontSize(13);
                            c.Item().Text("Comparativo de Cotizaciones de Seguros").FontSize(10).FontColor("#64748b");
                        });
                        row.ConstantItem(210).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Cliente: {comp.Cliente}").Bold();
                            if (!string.IsNullOrWhiteSpace(comp.Vehiculo))
                                c.Item().Text($"Vehículo: {comp.Vehiculo}");
                            c.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}").FontColor("#64748b");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1d4ed8");
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    // Tabla principal
                    col.Item().Table(tbl =>
                    {
                        tbl.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(115);
                            foreach (var _ in items) cd.RelativeColumn();
                        });

                        tbl.Header(h =>
                        {
                            h.Cell().Background("#1d4ed8").Padding(4)
                                .Text("Campo").Bold().FontColor(Colors.White).FontSize(8);
                            foreach (var item in items)
                                h.Cell().Background("#1d4ed8").Padding(4).AlignCenter()
                                    .Text(item.Aseguradora).Bold().FontColor(Colors.White).FontSize(8);
                        });

                        var rowIndex = 0;
                        AddRow("Posición",          items.Select(i => i.Posicion.HasValue ? $"#{i.Posicion}" : "—").ToList(), null);
                        AddRow("Score",              items.Select(i => i.Score.HasValue ? $"{i.Score:F1}/100" : "—").ToList(), bestScoreId);
                        AddRow("Prima anual",        items.Select(i => Lps(i.PrimaAnual)).ToList(), null);
                        AddRow("Prima contado",      items.Select(i => Lps(i.PrimaContado)).ToList(), bestPrimaId);
                        AddRow("Desc. contado",      items.Select(i => Pct(i.DescuentoContado, i.DescuentoEsPorcentaje)).ToList(), null);
                        AddRow("Ahorro al contado",  items.Select(i => Lps(i.AhorroContado)).ToList(), null);
                        AddRow("Prima financiada",   items.Select(i => Lps(i.PrimaFinanciada)).ToList(), null);
                        AddRow("Recargo financ.",    items.Select(i => Pct(i.RecargoFinanciamiento, i.RecargoEsPorcentaje)).ToList(), null);
                        AddRow("Prima mensual",      items.Select(i => Lps(i.PrimaMensual)).ToList(), null);
                        AddRow("Suma asegurada",     items.Select(i => Lps(i.SumaAsegurada)).ToList(), null);
                        AddRow("Deduc. colisión",    items.Select(i => Pct(i.DeducibleColision, i.DeducibleColisionEsPorcentaje)).ToList(), null);
                        AddRow("Deduc. robo total",  items.Select(i => Pct(i.DeducibleRobo, i.DeducibleRoboEsPorcentaje)).ToList(), null);
                        AddRow("Forma de pago",      items.Select(i => i.FormaPago ?? "—").ToList(), null);
                        AddRow("Vigencia",           items.Select(i => $"{i.VigenciaDesde ?? "—"} → {i.VigenciaHasta ?? "—"}").ToList(), null);

                        void AddRow(string label, List<string> vals, int? highlightId)
                        {
                            rowIndex++;
                            var bg = rowIndex % 2 == 0 ? "#f8fafc" : "#ffffff";
                            tbl.Cell().Background(bg).Padding(3).Text(label).Bold().FontSize(8);
                            for (var ci = 0; ci < items.Count; ci++)
                            {
                                bool hi = highlightId.HasValue && items[ci].Id == highlightId.Value;
                                tbl.Cell().Background(hi ? "#dcfce7" : bg).Padding(3).AlignCenter()
                                    .Text(vals[ci]).FontSize(8)
                                    .FontColor(hi ? "#15803d" : Colors.Black);
                            }
                        }
                    });

                    // Coberturas
                    var allCobs = items.SelectMany(i => i.Coberturas).Distinct().OrderBy(x => x).ToList();
                    if (allCobs.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("Coberturas incluidas").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(cd =>
                            {
                                cd.ConstantColumn(135);
                                foreach (var _ in items) cd.RelativeColumn();
                            });
                            tbl.Header(h =>
                            {
                                h.Cell().Background("#1d4ed8").Padding(3)
                                    .Text("Cobertura").Bold().FontColor(Colors.White).FontSize(8);
                                foreach (var item in items)
                                    h.Cell().Background("#1d4ed8").Padding(3).AlignCenter()
                                        .Text(item.Aseguradora).Bold().FontColor(Colors.White).FontSize(8);
                            });
                            var ri = 0;
                            foreach (var cob in allCobs)
                            {
                                ri++;
                                var bg = ri % 2 == 0 ? "#f8fafc" : "#ffffff";
                                tbl.Cell().Background(bg).Padding(3).Text(cob).FontSize(8);
                                foreach (var item in items)
                                {
                                    var has = item.Coberturas.Contains(cob);
                                    tbl.Cell().Background(has ? "#dcfce7" : bg).Padding(3).AlignCenter()
                                        .Text(has ? "✓" : "—").FontSize(9)
                                        .FontColor(has ? "#15803d" : "#94a3b8");
                                }
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(comp.Notas))
                        col.Item().PaddingTop(8).Background("#fef9c3").Padding(6)
                            .Text($"Notas: {comp.Notas}").FontSize(8).FontColor("#92400e");
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Generado por {empresa.NombreEmpresa} · ").FontSize(7).FontColor("#94a3b8");
                    t.CurrentPageNumber().FontSize(7).FontColor("#94a3b8");
                    t.Span(" / ").FontSize(7).FontColor("#94a3b8");
                    t.TotalPages().FontSize(7).FontColor("#94a3b8");
                });
            });
        });

        return File(pdf.GeneratePdf(), "application/pdf",
            $"comparativo_{comp.Cliente}.pdf");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    static string Lps(decimal? v) =>
        v.HasValue && v > 0 ? $"L {v:N2}" : "—";

    static string Pct(decimal? v, bool isPct) =>
        v.HasValue && v > 0 ? (isPct ? $"{v:F2}%" : $"L {v:N2}") : "—";
}

public record ComparativoRequest(
    string Cliente,
    string? Vehiculo,
    string? Notas,
    string? Estado);
