using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ReclamosWhatsApp.Services;

/// <summary>
/// Servicio reutilizable de exportación PDF basado en QuestPDF.
/// Genera documentos estructurados para cartera, clientes y reclamos.
/// </summary>
public class PdfService
{
    static PdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Reporte de cartera (lista de pólizas) ─────────────────────────────

    public byte[] GenerarReporteCartera(string titulo, IEnumerable<PolizaPdfRow> filas, string? filtro = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Element(header =>
                {
                    header.Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(titulo).FontSize(14).Bold().FontColor("#0f5f59");
                            if (filtro != null)
                                col.Item().Text(filtro).FontSize(9).FontColor("#6b7280");
                        });
                        row.ConstantItem(160).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#9ca3af");
                        });
                    });
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(30);   // #
                        cols.RelativeColumn(3);    // Cliente
                        cols.RelativeColumn(2);    // Póliza
                        cols.RelativeColumn(2);    // Aseguradora
                        cols.RelativeColumn(1.5f); // Ramo
                        cols.RelativeColumn(1.5f); // Vigencia
                        cols.RelativeColumn(1.5f); // Hasta
                        cols.RelativeColumn(1.5f); // Prima
                        cols.RelativeColumn(1.5f); // Estado
                    });

                    // Encabezados
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background("#172126").Padding(5).AlignCenter();

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "#", "Cliente", "Póliza", "Aseguradora", "Ramo", "Vigencia", "Hasta", "Prima", "Estado" })
                            h.Cell().Element(HeaderCell).Text(label).FontColor("#f8fafc").FontSize(8).Bold();
                    });

                    // Filas
                    var i = 0;
                    foreach (var f in filas)
                    {
                        i++;
                        var bg = i % 2 == 0 ? "#f2faf8" : "#ffffff";
                        static IContainer DataCell(IContainer c, string bg) =>
                            c.Background(bg).BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4);

                        table.Cell().Element(c => DataCell(c, bg)).Text($"{i}").FontColor("#6b7280");
                        table.Cell().Element(c => DataCell(c, bg)).Text(f.Cliente ?? "—");
                        table.Cell().Element(c => DataCell(c, bg)).Text(f.NumeroPoliza ?? "—");
                        table.Cell().Element(c => DataCell(c, bg)).Text(f.Aseguradora ?? "—");
                        table.Cell().Element(c => DataCell(c, bg)).Text(f.Ramo ?? "—");
                        table.Cell().Element(c => DataCell(c, bg)).AlignCenter().Text(f.Vigencia?.ToString("dd/MM/yy") ?? "—");
                        table.Cell().Element(c => DataCell(c, bg)).AlignCenter().Text(f.Hasta?.ToString("dd/MM/yy") ?? "—");
                        table.Cell().Element(c => DataCell(c, bg)).AlignRight().Text(f.PrimaTotal > 0 ? $"L {f.PrimaTotal:N0}" : "—");
                        table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                            .Text(f.Estado ?? "—")
                            .FontColor(f.Estado == "ACTIVA" ? "#166534" : f.Estado == "VENCIDA" ? "#991b1b" : "#92400e");
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontColor("#9ca3af").FontSize(8);
                    t.CurrentPageNumber().FontColor("#9ca3af").FontSize(8);
                    t.Span(" de ").FontColor("#9ca3af").FontSize(8);
                    t.TotalPages().FontColor("#9ca3af").FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    // ── Resumen de póliza (1 página) ──────────────────────────────────────

    public byte[] GenerarResumenPoliza(PolizaResumenPdf datos)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(h =>
                {
                    h.Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Resumen de Póliza").FontSize(18).Bold().FontColor("#0f5f59");
                            col.Item().Text(datos.EmpresaNombre ?? "Correduría de Seguros").FontSize(10).FontColor("#6b7280");
                        });
                        row.ConstantItem(120).Column(col =>
                        {
                            col.Item().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(9).FontColor("#9ca3af");
                        });
                    });
                    h.PaddingTop(6).LineHorizontal(1.5f).LineColor("#0f5f59");
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    static void SectionTitle(ColumnDescriptor col, string title)
                    {
                        col.Item().PaddingTop(12).PaddingBottom(4)
                            .Text(title).FontSize(11).Bold().FontColor("#172126");
                        col.Item().LineHorizontal(0.5f).LineColor("#d8e4e0");
                    }

                    static void Row(ColumnDescriptor col, string label, string? value)
                    {
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.ConstantItem(160).Text(label).FontColor("#6b7280");
                            row.RelativeItem().Text(value ?? "—");
                        });
                    }

                    SectionTitle(col, "Datos del Asegurado");
                    Row(col, "Cliente", datos.ClienteNombre);
                    Row(col, "Teléfono", datos.ClienteTelefono);
                    Row(col, "Correo", datos.ClienteEmail);

                    SectionTitle(col, "Datos de la Póliza");
                    Row(col, "Número de póliza", datos.NumeroPoliza);
                    Row(col, "Aseguradora", datos.Aseguradora);
                    Row(col, "Ramo", datos.Ramo);
                    Row(col, "Vigencia", datos.Vigencia?.ToString("dd/MM/yyyy"));
                    Row(col, "Vencimiento", datos.Hasta?.ToString("dd/MM/yyyy"));
                    Row(col, "Prima total", datos.PrimaTotal > 0 ? $"L {datos.PrimaTotal:N2}" : null);
                    Row(col, "Forma de pago", datos.FormaPago);
                    Row(col, "Estado", datos.Estado);

                    if (!string.IsNullOrWhiteSpace(datos.Vehiculo))
                    {
                        SectionTitle(col, "Vehículo Asegurado");
                        Row(col, "Descripción", datos.Vehiculo);
                        Row(col, "Placa", datos.Placa);
                    }

                    if (datos.Cuotas?.Count > 0)
                    {
                        SectionTitle(col, "Plan de Cuotas");
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.ConstantColumn(40); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h =>
                            {
                                foreach (var label in new[] { "N°", "Vencimiento", "Monto", "Estado" })
                                    h.Cell().Background("#172126").Padding(4).Text(label).FontColor("#f8fafc").FontSize(8).Bold();
                            });
                            foreach (var c in datos.Cuotas)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4).Text($"{c.Numero}");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4).Text(c.Fecha?.ToString("dd/MM/yyyy") ?? "—");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4).AlignRight().Text(c.Monto > 0 ? $"L {c.Monto:N2}" : "—");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4).AlignCenter()
                                    .Text(c.Estado ?? "—")
                                    .FontColor(c.Estado == "PAGADA" ? "#166534" : c.Estado == "VENCIDA" ? "#991b1b" : "#374151");
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Documento generado el ").FontColor("#9ca3af").FontSize(8);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontColor("#9ca3af").FontSize(8);
                    t.Span(" — Confidencial").FontColor("#9ca3af").FontSize(8);
                });
            });
        }).GeneratePdf();
    }
}

// ── DTOs ────────────────────────────────────────────────────────────────────

public record PolizaPdfRow(
    string? Cliente, string? NumeroPoliza, string? Aseguradora,
    string? Ramo, DateTime? Vigencia, DateTime? Hasta,
    decimal PrimaTotal, string? Estado);

public class PolizaResumenPdf
{
    public string? EmpresaNombre { get; init; }
    public string? ClienteNombre { get; init; }
    public string? ClienteTelefono { get; init; }
    public string? ClienteEmail { get; init; }
    public string? NumeroPoliza { get; init; }
    public string? Aseguradora { get; init; }
    public string? Ramo { get; init; }
    public DateTime? Vigencia { get; init; }
    public DateTime? Hasta { get; init; }
    public decimal PrimaTotal { get; init; }
    public string? FormaPago { get; init; }
    public string? Estado { get; init; }
    public string? Vehiculo { get; init; }
    public string? Placa { get; init; }
    public List<CuotaPdf>? Cuotas { get; init; }
}

public record CuotaPdf(int Numero, DateTime? Fecha, decimal Monto, string? Estado);
