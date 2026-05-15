using System.ComponentModel.DataAnnotations;

namespace ReclamosWhatsApp.Models;

public class CorreoExtractorConfig
{
    [Required]
    public string SubjectPattern { get; set; } =
        @"(?<placa>[A-Z]{2,4}-?[0-9]{3,4})\s*,?\s+(?<reclamo>[A-Z]{2,5}-[0-9]{2,5}-[0-9]{4})";

    [Required]
    public string AseguradoPattern { get; set; } = @"Asegurado\s*:\s*(.+)";

    [Required]
    public string PolizaPattern { get; set; } = @"(?:No\.\s*)?P[oó]liza\s*:\s*(.+)";

    [Required]
    public string CaracteristicasPattern { get; set; } = @"(?:Caracter[ií]sticas del Bien asegurado|Veh[ií]culo|Vehiculo)\s*:\s*(.+)";

    [Required]
    public string ConductorPattern { get; set; } = @"Conductor\s*:\s*(.+)";

    [Required]
    public string CelularPattern { get; set; } = @"Celular\s*:\s*([0-9\-\s\+]+)";

    [Required]
    public string FechaPattern { get; set; } = @"(?:notificado con fecha|Fecha(?: de notificacion| notificacion| de aviso| del accidente| de accidente| del siniestro| de siniestro)?)\s*:?\s*([0-9]{1,2}[\/\-][0-9]{1,2}[\/\-][0-9]{4})";

    [Required]
    public string LugarPattern { get; set; } = @"(?:Ocurrido en|Ocurrio en|Ocurri[oó] en|Lugar(?: del accidente| de accidente| del siniestro| de siniestro)?|Accidente ocurrido en|Siniestro ocurrido en|Direcci[oó]n del accidente|Ubicaci[oó]n|Ciudad|Municipio|Localidad)\s*:?\s*([^\r\n]+)";

    public static IReadOnlyDictionary<string, string> DefaultValues => new Dictionary<string, string>
    {
        [nameof(SubjectPattern)] = @"(?<placa>[A-Z]{2,4}-?[0-9]{3,4})\s*,?\s+(?<reclamo>[A-Z]{2,5}-[0-9]{2,5}-[0-9]{4})",
        [nameof(AseguradoPattern)] = @"Asegurado\s*:\s*(.+)",
        [nameof(PolizaPattern)] = @"(?:No\.\s*)?P[oó]liza\s*:\s*(.+)",
        [nameof(CaracteristicasPattern)] = @"(?:Caracter[ií]sticas del Bien asegurado|Veh[ií]culo|Vehiculo)\s*:\s*(.+)",
        [nameof(ConductorPattern)] = @"Conductor\s*:\s*(.+)",
        [nameof(CelularPattern)] = @"Celular\s*:\s*([0-9\-\s\+]+)",
        [nameof(FechaPattern)] = @"(?:notificado con fecha|Fecha(?: de notificacion| notificacion| de aviso| del accidente| de accidente| del siniestro| de siniestro)?)\s*:?\s*([0-9]{1,2}[\/\-][0-9]{1,2}[\/\-][0-9]{4})",
        [nameof(LugarPattern)] = @"(?:Ocurrido en|Ocurrio en|Ocurri[oó] en|Lugar(?: del accidente| de accidente| del siniestro| de siniestro)?|Accidente ocurrido en|Siniestro ocurrido en|Direcci[oó]n del accidente|Ubicaci[oó]n|Ciudad|Municipio|Localidad)\s*:?\s*([^\r\n]+)"
    };
}
