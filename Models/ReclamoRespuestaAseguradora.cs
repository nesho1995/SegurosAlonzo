namespace ReclamosWhatsApp.Models;

public class ReclamoRespuestaAseguradora
{
    public int Id { get; set; }
    public int ReclamoId { get; set; }
    public string Origen { get; set; } = "MANUAL";
    public string? Remitente { get; set; }
    public string? Asunto { get; set; }
    public string Respuesta { get; set; } = "";
    public bool Aprobado { get; set; }
    public bool RequiereRsa { get; set; }
    public bool RequiereDeducible { get; set; }
    public bool SolicitaMasDocumentos { get; set; }
    public bool AprobadoSinPagosFinales { get; set; }
    public string? Acciones { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime CreadoEn { get; set; }
}
