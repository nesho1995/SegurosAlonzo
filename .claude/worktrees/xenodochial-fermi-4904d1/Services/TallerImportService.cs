using System.Text;
using ClosedXML.Excel;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class TallerImportService
{
    private static readonly string[] Headers =
    [
        "Nombre", "Ciudad", "Zona", "Direccion", "Telefono", "WhatsApp", "Email", "Contacto",
        "Aseguradoras", "Ramos", "EsPreferido", "OrdenPrioridad", "Activo", "Observaciones"
    ];

    public List<TallerImportPreview> Preview(Stream archivo, string fileName = "")
    {
        if (Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return PreviewCsv(archivo);

        return PreviewExcel(archivo);
    }

    public byte[] CrearPlantillaCsv()
    {
        var lines = new[]
        {
            string.Join(",", Headers),
            "Taller San Pedro Centro,SAN PEDRO SULA,CENTRO,Barrio Guamilito,25551234,50499999999,taller@correo.com,Carlos Mejia,CREFISA;FICOHSA,AUTOS;MOTOS,SI,1,SI,Taller recomendado"
        };
        return Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
    }

    public byte[] CrearPlantilla()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Talleres");

        for (var i = 0; i < Headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = Headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F2EF");
        }

        var sample = "Taller San Pedro Centro,SAN PEDRO SULA,CENTRO,Barrio Guamilito,25551234,50499999999,taller@correo.com,Carlos Mejia,CREFISA;FICOHSA,AUTOS;MOTOS,SI,1,SI,Taller recomendado".Split(',');
        for (var i = 0; i < sample.Length; i++)
            ws.Cell(2, i + 1).Value = sample[i];

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static List<TallerImportPreview> PreviewExcel(Stream archivo)
    {
        using var workbook = new XLWorkbook(archivo);
        var ws = workbook.Worksheets.First();
        var map = new Dictionary<string, int>();

        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var header = NormalizarHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(header))
                map[header] = cell.Address.ColumnNumber;
        }

        var result = new List<TallerImportPreview>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var item = BuildPreview(row.RowNumber(), key => Get(row, map, key));
            if (!string.IsNullOrWhiteSpace(item.Taller.Nombre) || item.Errores.Count > 0)
                result.Add(item);
        }

        return result;
    }

    private static List<TallerImportPreview> PreviewCsv(Stream archivo)
    {
        using var reader = new StreamReader(archivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<string[]>();
        while (!reader.EndOfStream)
            rows.Add(ParseCsvLine(reader.ReadLine() ?? ""));

        if (rows.Count == 0)
            return [];

        var map = rows[0]
            .Select((header, index) => new { Key = NormalizarHeader(header), Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x.Index);

        var result = new List<TallerImportPreview>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var item = BuildPreview(i + 1, key => map.TryGetValue(NormalizarHeader(key), out var index) && index < row.Length ? row[index].Trim() : "");
            if (!string.IsNullOrWhiteSpace(item.Taller.Nombre) || item.Errores.Count > 0)
                result.Add(item);
        }

        return result;
    }

    private static TallerImportPreview BuildPreview(int fila, Func<string, string> get)
    {
        var taller = new Taller
        {
            Nombre = get("Nombre"),
            Ciudad = TallerRepository.Normalize(get("Ciudad")),
            Zona = TallerRepository.Normalize(get("Zona")),
            Direccion = get("Direccion"),
            Telefono = get("Telefono"),
            WhatsApp = get("WhatsApp"),
            Email = get("Email"),
            Contacto = get("Contacto"),
            AseguradorasAceptadas = TallerRepository.SplitMulti(get("Aseguradoras")).Select(TallerRepository.Normalize).ToList(),
            RamosAtendidos = TallerRepository.SplitMulti(get("Ramos")).Select(TallerRepository.NormalizeRamo).ToList(),
            EsPreferido = ParseBool(get("EsPreferido")),
            OrdenPrioridad = int.TryParse(get("OrdenPrioridad"), out var orden) ? orden : 100,
            Activo = string.IsNullOrWhiteSpace(get("Activo")) || ParseBool(get("Activo")),
            Observaciones = get("Observaciones")
        };
        taller.Aseguradora = taller.AseguradorasAceptadas.FirstOrDefault() ?? "";
        taller.Ramo = taller.RamosAtendidos.FirstOrDefault();

        var item = new TallerImportPreview { Fila = fila, Taller = taller };
        if (string.IsNullOrWhiteSpace(taller.Nombre))
            item.Errores.Add("El nombre del taller es requerido.");
        if (string.IsNullOrWhiteSpace(taller.Ciudad))
            item.Errores.Add("La ciudad es requerida.");
        if (taller.AseguradorasAceptadas.Count == 0)
            item.Errores.Add("Agrega al menos una aseguradora.");
        if (taller.RamosAtendidos.Count == 0)
            item.Errores.Add("Agrega al menos un ramo.");
        return item;
    }

    private static string Get(IXLRow row, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(NormalizarHeader(key), out var col)
            ? row.Cell(col).GetFormattedString().Trim()
            : "";
    }

    private static bool ParseBool(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant() is "SI" or "SÍ" or "YES" or "TRUE" or "1" or "ACTIVO";
    }

    private static string NormalizarHeader(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant().Replace(" ", "");
    }

    private static string[] ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (ch == ',' && !quoted)
            {
                cells.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(ch);
        }
        cells.Add(sb.ToString());
        return cells.ToArray();
    }
}
