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
        QuestPDF.Settings.UseEnvironmentFonts = true;
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
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily(Fonts.Lato));

                page.Header().Column(hcol =>
                {
                    hcol.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(t => t.Span(titulo).FontSize(14).Bold().FontColor("#0f5f59"));
                            if (filtro != null)
                                col.Item().Text(t => t.Span(filtro).FontSize(9).FontColor("#6b7280"));
                        });
                        row.ConstantItem(160).AlignRight().Column(col =>
                        {
                            col.Item().Text(t => t.Span($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#9ca3af"));
                        });
                    });
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(30);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                    });

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "#", "Cliente", "Póliza", "Aseguradora", "Ramo", "Vigencia", "Hasta", "Prima", "Estado" })
                            h.Cell().Background("#172126").Padding(5).AlignCenter()
                                .Text(t => t.Span(label).FontColor("#f8fafc").FontSize(8).Bold());
                    });

                    var rowIndex = 0;
                    foreach (var fila in filas)
                    {
                        rowIndex++;
                        var bg = rowIndex % 2 == 0 ? "#f2faf8" : "#ffffff";

                        void DataCell(IContainer cell, Action<IContainer> content)
                        {
                            content(cell.Background(bg).BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4));
                        }

                        DataCell(table.Cell(), c => c.Text(t => t.Span($"{rowIndex}").FontColor("#6b7280")));
                        DataCell(table.Cell(), c => c.Text(fila.Cliente ?? "—"));
                        DataCell(table.Cell(), c => c.Text(fila.NumeroPoliza ?? "—"));
                        DataCell(table.Cell(), c => c.Text(fila.Aseguradora ?? "—"));
                        DataCell(table.Cell(), c => c.Text(fila.Ramo ?? "—"));
                        DataCell(table.Cell(), c => c.AlignCenter().Text(fila.Vigencia?.ToString("dd/MM/yy") ?? "—"));
                        DataCell(table.Cell(), c => c.AlignCenter().Text(fila.Hasta?.ToString("dd/MM/yy") ?? "—"));
                        DataCell(table.Cell(), c => c.AlignRight().Text(fila.PrimaTotal > 0 ? $"L {fila.PrimaTotal:N0}" : "—"));

                        var estadoColor = fila.Estado == "ACTIVA" ? "#166534" : fila.Estado == "VENCIDA" ? "#991b1b" : "#92400e";
                        DataCell(table.Cell(), c => c.AlignCenter().Text(t => t.Span(fila.Estado ?? "—").FontColor(estadoColor)));
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
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily(Fonts.Lato));

                // Header: Column wraps Row + separator line (two children need Column)
                page.Header().Column(hcol =>
                {
                    hcol.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(t => t.Span("Resumen de Póliza").FontSize(18).Bold().FontColor("#0f5f59"));
                            col.Item().Text(t => t.Span(datos.EmpresaNombre ?? "Correduría de Seguros").FontSize(10).FontColor("#6b7280"));
                        });
                        row.ConstantItem(120).AlignRight()
                            .Text(t => t.Span(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(9).FontColor("#9ca3af"));
                    });
                    hcol.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor("#0f5f59");
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    void SectionTitle(string title)
                    {
                        col.Item().PaddingTop(12).PaddingBottom(2)
                            .Text(t => t.Span(title).FontSize(11).Bold().FontColor("#172126"));
                        col.Item().LineHorizontal(0.5f).LineColor("#d8e4e0");
                    }

                    void InfoRow(string label, string? value)
                    {
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.ConstantItem(160).Text(t => t.Span(label).FontColor("#6b7280"));
                            row.RelativeItem().Text(value ?? "—");
                        });
                    }

                    SectionTitle("Datos del Asegurado");
                    InfoRow("Cliente", datos.ClienteNombre);
                    InfoRow("Teléfono", datos.ClienteTelefono);
                    InfoRow("Correo", datos.ClienteEmail);

                    SectionTitle("Datos de la Póliza");
                    InfoRow("Número de póliza", datos.NumeroPoliza);
                    InfoRow("Aseguradora", datos.Aseguradora);
                    InfoRow("Ramo", datos.Ramo);
                    InfoRow("Vigencia", datos.Vigencia?.ToString("dd/MM/yyyy"));
                    InfoRow("Vencimiento", datos.Hasta?.ToString("dd/MM/yyyy"));
                    InfoRow("Prima total", datos.PrimaTotal > 0 ? $"L {datos.PrimaTotal:N2}" : null);
                    InfoRow("Forma de pago", datos.FormaPago);
                    InfoRow("Estado", datos.Estado);

                    if (!string.IsNullOrWhiteSpace(datos.Vehiculo))
                    {
                        SectionTitle("Vehículo Asegurado");
                        InfoRow("Descripción", datos.Vehiculo);
                        InfoRow("Placa", datos.Placa);
                    }

                    if (datos.Cuotas?.Count > 0)
                    {
                        SectionTitle("Plan de Cuotas");
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(def =>
                            {
                                def.ConstantColumn(40);
                                def.RelativeColumn();
                                def.RelativeColumn();
                                def.RelativeColumn();
                            });

                            table.Header(h =>
                            {
                                foreach (var label in new[] { "N°", "Vencimiento", "Monto", "Estado" })
                                    h.Cell().Background("#172126").Padding(4)
                                        .Text(t => t.Span(label).FontColor("#f8fafc").FontSize(8).Bold());
                            });

                            foreach (var cuota in datos.Cuotas)
                            {
                                var estadoColor = cuota.Estado == "PAGADA" ? "#166534"
                                    : cuota.Estado == "VENCIDA" ? "#991b1b"
                                    : "#374151";

                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4)
                                    .Text($"{cuota.Numero}");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4)
                                    .Text(cuota.Fecha?.ToString("dd/MM/yyyy") ?? "—");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4).AlignRight()
                                    .Text(cuota.Monto > 0 ? $"L {cuota.Monto:N2}" : "—");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e5eeec").Padding(4).AlignCenter()
                                    .Text(t => t.Span(cuota.Estado ?? "—").FontColor(estadoColor));
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
