namespace ReclamosWhatsApp.Models;

public class WhatsAppConversacion
{
    public int Id { get; set; }
    public string Telefono { get; set; } = "";
    public string? NombreContacto { get; set; }
    public int? ClienteId { get; set; }
    public string Estado { get; set; } = "abierta";
    public DateTime UltimaActividad { get; set; }
    public int NoLeidos { get; set; }
    public int? AgenteAsignadoId { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class WhatsAppMensaje
{
    public int Id { get; set; }
    public int ConversacionId { get; set; }
    public string? WhatsappMessageId { get; set; }
    public string Direccion { get; set; } = "entrante";
    public string TipoContenido { get; set; } = "texto";
    public string? Contenido { get; set; }
    public string? MediaId { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaTipoMime { get; set; }
    public string? MediaNombre { get; set; }
    public string Estado { get; set; } = "recibido";
    public int? UsuarioId { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class ConversacionListItem
{
    public int Id { get; set; }
    public string Telefono { get; set; } = "";
    public string? NombreContacto { get; set; }
    public int? ClienteId { get; set; }
    public string? NombreCliente { get; set; }
    public string Estado { get; set; } = "abierta";
    public DateTime UltimaActividad { get; set; }
    public int NoLeidos { get; set; }
    public string? UltimoMensaje { get; set; }
    public string? UltimoDireccion { get; set; }
}

public class MensajeDto
{
    public int Id { get; set; }
    public string Direccion { get; set; } = "entrante";
    public string TipoContenido { get; set; } = "texto";
    public string? Contenido { get; set; }
    public string? MediaId { get; set; }
    public string? MediaTipoMime { get; set; }
    public string? MediaNombre { get; set; }
    public string Estado { get; set; } = "recibido";
    public string? NombreUsuario { get; set; }
    public DateTime CreadoEn { get; set; }
}
