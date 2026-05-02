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
using System.Security.Claims;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.CotizacionesVer)]
[Route("api/cotizaciones")]
public class CotizacionApiController : ControllerBase
{
    private readonly CotizacionRepository _repo;
    private readonly AuditoriaService _auditoria;

    public CotizacionApiController(CotizacionRepository repo, AuditoriaService auditoria)
    {
        _repo     = repo;
        _auditoria = auditoria;
    }

    private string UserName =>
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? "sistema";

    // ── GET /api/cotizaciones ─────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Get(
        string? estado = "TODOS",
        string? buscar = null,
        int pagina = 1,
        int pageSize = 25)
    {
        var (items, total) = await _repo.GetAsync(estado, buscar, pagina, pageSize);
        return Ok(new
        {
            items,
            total,
            pagina,
            pageSize,
            totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize))
        });
    }

    // ── GET /api/cotizaciones/{id} ────────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        var detalle = await _repo.GetDetalleAsync(id);
        if (detalle is null) return NotFound(new { error = "Cotización no encontrada." });
        return Ok(detalle);
    }

    // ── POST /api/cotizaciones ────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = Permissions.CotizacionesCrear)]
    public async Task<IActionResult> Crear([FromBody] Cotizacion body)
    {
        if (string.IsNullOrWhiteSpace(body.ClienteNombre) && !body.ClienteId.HasValue)
            return BadRequest(new { error = "Debe indicar un cliente o nombre." });
        if (string.IsNullOrWhiteSpace(body.Ramo))
            return BadRequest(new { error = "El ramo es obligatorio." });

        body.CreadoPor = UserName;
        body.Estado    = "BORRADOR";
        body.Activo    = true;
        var id = await _repo.CrearAsync(body);
        await _auditoria.RegistrarAsync("CREAR_COTIZACION", "COTIZACION", id,
            $"Cotización creada para '{body.ClienteNombre}', ramo: {body.Ramo}.");
        return Ok(new { id });
    }

    // ── PUT /api/cotizaciones/{id} ────────────────────────────────────────

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] Cotizacion body)
    {
        var ok = await _repo.ActualizarAsync(id, body);
        if (!ok) return NotFound(new { error = "Cotización no encontrada." });
        await _auditoria.RegistrarAsync("EDITAR_COTIZACION", "COTIZACION", id,
            $"Cotización actualizada. Estado: {body.Estado}.");
        return Ok(new { ok = true });
    }

    // ── DELETE /api/cotizaciones/{id} ────────────────────────────────────

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CotizacionesEliminar)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var ok = await _repo.EliminarAsync(id);
        if (!ok) return NotFound(new { error = "Cotización no encontrada." });
        await _auditoria.RegistrarAsync("ELIMINAR_COTIZACION", "COTIZACION", id,
            "Cotización eliminada (soft delete).");
        return Ok(new { ok = true });
    }

    // ── POST /api/cotizaciones/{id}/items ────────────────────────────────

    [HttpPost("{id:int}/items")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> AgregarItem(int id, [FromBody] CotizacionItem body)
    {
        if (string.IsNullOrWhiteSpace(body.Aseguradora))
            return BadRequest(new { error = "El campo aseguradora es obligatorio." });

        body.CotizacionId = id;
        var itemId = await _repo.AgregarItemAsync(body);
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { id = itemId });
    }

    // ── PUT /api/cotizaciones/{id}/items/{itemId} ────────────────────────

    [HttpPut("{id:int}/items/{itemId:int}")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> ActualizarItem(int id, int itemId, [FromBody] CotizacionItem body)
    {
        var ok = await _repo.ActualizarItemAsync(itemId, body);
        if (!ok) return NotFound(new { error = "Item no encontrado." });
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ── DELETE /api/cotizaciones/{id}/items/{itemId} ─────────────────────

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> EliminarItem(int id, int itemId)
    {
        var ok = await _repo.EliminarItemAsync(itemId);
        if (!ok) return NotFound(new { error = "Item no encontrado." });
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ── POST /api/cotizaciones/{id}/items/{itemId}/coberturas ─────────────

    [HttpPost("{id:int}/items/{itemId:int}/coberturas")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> GuardarCoberturas(
        int id, int itemId, [FromBody] List<CotizacionCobertura> body)
    {
        await _repo.GuardarCoberturasAsync(itemId, body);
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ── POST /api/cotizaciones/{id}/items/{itemId}/exclusiones ────────────

    [HttpPost("{id:int}/items/{itemId:int}/exclusiones")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> GuardarExclusiones(
        int id, int itemId, [FromBody] List<CotizacionExclusion> body)
    {
        await _repo.GuardarExclusionesAsync(itemId, body);
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ── POST /api/cotizaciones/{id}/recalcular ────────────────────────────

    [HttpPost("{id:int}/recalcular")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> Recalcular(int id)
    {
        await _repo.RecalcularRankingAsync(id);
        return Ok(new { ok = true });
    }

    // ── POST /api/cotizaciones/{id}/analisis ──────────────────────────────

    [HttpPost("{id:int}/analisis")]
    [Authorize(Policy = Permissions.CotizacionesEditar)]
    public async Task<IActionResult> GuardarAnalisis(int id, [FromBody] CotizacionAnalisis body)
    {
        body.CotizacionId = id;
        body.CreadoPor    = UserName;

        int analisisId;
        if (body.Id > 0)
        {
            await _repo.ActualizarAnalisisAsync(body.Id, body);
            analisisId = body.Id;
        }
        else
        {
            analisisId = await _repo.GuardarAnalisisAsync(body);
        }
        return Ok(new { id = analisisId });
    }

    // ── GET /api/cotizaciones/{id}/excel ──────────────────────────────────

    [HttpGet("{id:int}/excel")]
    public async Task<IActionResult> ExportarExcel(int id)
    {
        var detalle = await _repo.GetDetalleAsync(id);
        if (detalle is null) return NotFound(new { error = "Cotización no encontrada." });

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Comparativa");

        // Header row
        var teal = XLColor.FromHtml("#0f5f59");
        ws.Row(1).Height = 18;
        ws.Cell(1, 1).Value = "Campo";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Fill.BackgroundColor = teal;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;

        var items = detalle.Items.ToList();
        for (var col = 0; col < items.Count; col++)
        {
            var cell = ws.Cell(1, col + 2);
            cell.Value = items[col].Aseguradora + (items[col].Plan != null ? $"\n{items[col].Plan}" : "");
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = teal;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.WrapText = true;

            if (items[col].Recomendado)
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#166649");
            }
        }

        // Data rows
        var fields = new[]
        {
            ("Prima anual",     (CotizacionItem i) => (object?)(i.PrimaAnual.HasValue ? $"L {i.PrimaAnual:N2}" : "—")),
            ("Prima mensual",   (CotizacionItem i) => (object?)(i.PrimaMensual.HasValue ? $"L {i.PrimaMensual:N2}" : "—")),
            ("Frecuencia",      (CotizacionItem i) => (object?)i.FrecuenciaPago),
            ("Suma asegurada",  (CotizacionItem i) => (object?)(i.SumaAsegurada.HasValue ? $"L {i.SumaAsegurada:N2}" : "—")),
            ("Deducible",       (CotizacionItem i) => (object?)(i.Deducible.HasValue ? $"L {i.Deducible:N2}" : "—")),
            ("Vigencia (meses)",(CotizacionItem i) => (object?)(i.VigenciaMeses?.ToString() ?? "—")),
            ("Ranking (pts)",   (CotizacionItem i) => (object?)(i.RankingPuntos.HasValue ? $"{i.RankingPuntos:N1}" : "—")),
            ("Posición",        (CotizacionItem i) => (object?)(i.RankingPosicion.HasValue ? $"#{i.RankingPosicion}" : "—")),
        };

        for (var r = 0; r < fields.Length; r++)
        {
            var row = ws.Row(r + 2);
            ws.Cell(r + 2, 1).Value = fields[r].Item1;
            ws.Cell(r + 2, 1).Style.Font.Bold = true;
            ws.Cell(r + 2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#f0f9f7");

            for (var c = 0; c < items.Count; c++)
            {
                ws.Cell(r + 2, c + 2).Value = fields[r].Item2(items[c])?.ToString() ?? "—";
            }
        }

        // Coberturas section
        int startRow = fields.Length + 3;
        ws.Cell(startRow, 1).Value = "COBERTURAS";
        ws.Cell(startRow, 1).Style.Font.Bold = true;
        ws.Cell(startRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#e0f2ee");
        ws.Range(startRow, 1, startRow, items.Count + 1).Merge();

        startRow++;
        var allCobNames = items
            .SelectMany(i => i.Coberturas.Select(c => c.Nombre))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        foreach (var cobName in allCobNames)
        {
            ws.Cell(startRow, 1).Value = cobName;
            for (var c = 0; c < items.Count; c++)
            {
                var cob = items[c].Coberturas.FirstOrDefault(
                    x => string.Equals(x.Nombre, cobName, StringComparison.OrdinalIgnoreCase));
                if (cob is null)
                {
                    ws.Cell(startRow, c + 2).Value = "No incluye";
                    ws.Cell(startRow, c + 2).Style.Font.FontColor = XLColor.Red;
                }
                else if (!cob.Aplica)
                {
                    ws.Cell(startRow, c + 2).Value = "Excluida";
                    ws.Cell(startRow, c + 2).Style.Font.FontColor = XLColor.Orange;
                }
                else
                {
                    ws.Cell(startRow, c + 2).Value = cob.Limite ?? "Incluida";
                    ws.Cell(startRow, c + 2).Style.Font.FontColor = XLColor.FromHtml("#166649");
                }
            }
            startRow++;
        }

        ws.Columns().AdjustToContents(1, 80);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fname = $"cotizacion_{detalle.Cotizacion.ClienteNombre.Replace(" ", "_")}_{id}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fname);
    }

    // ── GET /api/cotizaciones/{id}/pdf ────────────────────────────────────

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> ExportarPdf(int id)
    {
        var detalle = await _repo.GetDetalleAsync(id);
        if (detalle is null) return NotFound(new { error = "Cotización no encontrada." });

        var pdf = GenerarPdf(detalle);
        var fname = $"cotizacion_{id}.pdf";
        return File(pdf, "application/pdf", fname);
    }

    // ── PDF generation ────────────────────────────────────────────────────

    private static byte[] GenerarPdf(CotizacionDetalle detalle)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var cot   = detalle.Cotizacion;
        var items = detalle.Items.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                // Header
                page.Header().BorderBottom(1).BorderColor("#e2ebe8").PaddingBottom(6).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Comparativa de Cotizaciones — {cot.Ramo}")
                            .FontSize(13).Bold().FontColor("#0f5f59");
                        col.Item().Text($"Cliente: {cot.ClienteNombre}   |   Estado: {cot.Estado}")
                            .FontSize(9).FontColor("#6b7280");
                    });
                    row.ConstantItem(160).AlignRight()
                        .Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor("#9ca3af");
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    // Summary table
                    col.Item().Table(table =>
                    {
                        var colCount = items.Count + 1;
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            for (var i = 0; i < items.Count; i++)
                                cols.RelativeColumn();
                        });

                        // Header cells
                        table.Cell().Background("#0f5f59").Padding(5)
                            .Text("Campo").FontColor("#ffffff").Bold().FontSize(8);

                        foreach (var item in items)
                        {
                            var bg = item.Recomendado ? "#166649" : "#0f5f59";
                            var label = item.Aseguradora + (item.Recomendado ? " ★" : "");
                            table.Cell().Background(bg).Padding(5)
                                .Text(label).FontColor("#ffffff").Bold().FontSize(8);
                        }

                        // Rows
                        void DataRow(string label, Func<CotizacionItem, string> fn, bool shade)
                        {
                            var bg = shade ? "#f5faf8" : "#ffffff";
                            table.Cell().Background(bg).Padding(4).Text(label).Bold().FontSize(8);
                            foreach (var item in items)
                                table.Cell().Background(bg).Padding(4).Text(fn(item)).FontSize(8);
                        }

                        DataRow("Prima anual",
                            i => i.PrimaAnual.HasValue ? $"L {i.PrimaAnual:N2}" : "—", false);
                        DataRow("Prima mensual",
                            i => i.PrimaMensual.HasValue ? $"L {i.PrimaMensual:N2}" : "—", true);
                        DataRow("Frecuencia",      i => i.FrecuenciaPago, false);
                        DataRow("Suma asegurada",
                            i => i.SumaAsegurada.HasValue ? $"L {i.SumaAsegurada:N2}" : "—", true);
                        DataRow("Deducible",
                            i => i.Deducible.HasValue ? $"L {i.Deducible:N2}" : "—", false);
                        DataRow("Vigencia",
                            i => i.VigenciaMeses.HasValue ? $"{i.VigenciaMeses} meses" : "—", true);
                        DataRow("Ranking",
                            i => i.RankingPuntos.HasValue ? $"{i.RankingPuntos:N1} pts (#{i.RankingPosicion})" : "—",
                            false);
                    });

                    // Coberturas
                    if (items.Any(i => i.Coberturas.Any()))
                    {
                        col.Item().PaddingTop(12).Text("Coberturas").FontSize(10).Bold().FontColor("#0f5f59");
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                for (var i = 0; i < items.Count; i++)
                                    cols.RelativeColumn();
                            });

                            table.Cell().Background("#0f5f59").Padding(4)
                                .Text("Cobertura").FontColor("#ffffff").Bold().FontSize(8);
                            foreach (var item in items)
                                table.Cell().Background("#0f5f59").Padding(4)
                                    .Text(item.Aseguradora).FontColor("#ffffff").Bold().FontSize(8);

                            var allCobs = items.SelectMany(i => i.Coberturas.Select(c => c.Nombre))
                                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

                            var shade = false;
                            foreach (var cobName in allCobs)
                            {
                                var bg = shade ? "#f5faf8" : "#ffffff";
                                table.Cell().Background(bg).Padding(4).Text(cobName).FontSize(8);
                                foreach (var item in items)
                                {
                                    var cob = item.Coberturas.FirstOrDefault(
                                        x => string.Equals(x.Nombre, cobName, StringComparison.OrdinalIgnoreCase));
                                    string val;
                                    string color;
                                    if (cob is null)             { val = "No incluye"; color = "#ef4444"; }
                                    else if (!cob.Aplica)        { val = "Excluida";   color = "#f97316"; }
                                    else { val = cob.Limite ?? "Incluida"; color = "#166649"; }
                                    table.Cell().Background(bg).Padding(4)
                                        .Text(val).FontSize(8).FontColor(color);
                                }
                                shade = !shade;
                            }
                        });
                    }

                    // Análisis
                    if (detalle.Analisis?.AnalisisTexto is { Length: > 0 } texto)
                    {
                        col.Item().PaddingTop(12).Text("Análisis").FontSize(10).Bold().FontColor("#0f5f59");
                        col.Item().Background("#f5faf8").Padding(8)
                            .Text(texto).FontSize(8).FontColor("#374151");

                        if (detalle.Analisis.Recomendacion is { Length: > 0 } rec)
                        {
                            col.Item().PaddingTop(6)
                                .Text($"Recomendación: {rec}").FontSize(9).Bold().FontColor("#166649");
                        }
                    }
                });

                page.Footer().AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ").FontSize(8).FontColor("#9ca3af");
                        x.CurrentPageNumber().FontSize(8).FontColor("#9ca3af");
                        x.Span(" de ").FontSize(8).FontColor("#9ca3af");
                        x.TotalPages().FontSize(8).FontColor("#9ca3af");
                    });
            });
        }).GeneratePdf();
    }
}
