namespace ReclamosWhatsApp.Models;

public class Documento
{
    public int Id { get; set; }
    public string EntidadTipo { get; set; } = string.Empty;
    public int EntidadId { get; set; }
    public string NombreArchivoOriginal { get; set; } = string.Empty;
    public string NombreArchivoGuardado { get; set; } = string.Empty;
    public string RutaRelativa { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public string MimeType { get; set; } = "application/octet-stream";
    public long TamanoBytes { get; set; }
    public string? HashArchivo { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaSubida { get; set; }
    public int? SubidoPorUsuarioId { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string? Usuario { get; set; }
}

public class DocumentoDto
{
    public int Id { get; set; }
    public string EntidadTipo { get; set; } = string.Empty;
    public int EntidadId { get; set; }
    public string NombreArchivoOriginal { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public DateTime FechaSubida { get; set; }
    public int? SubidoPorUsuarioId { get; set; }
    public string Usuario { get; set; } = "Sistema";
    public long TamanoBytes { get; set; }
    public string MimeType { get; set; } = "application/octet-stream";
    public string Extension { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string VerUrl => $"/api/documentos/{Id}/ver";
    public string DescargarUrl => $"/api/documentos/{Id}/descargar";
    public string DownloadUrl => DescargarUrl;
}

public class AuditoriaLog
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string? Usuario { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string EntidadTipo { get; set; } = string.Empty;
    public int? EntidadId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Ip { get; set; }
}

public class AuditoriaFiltro
{
    public int? UsuarioId { get; set; }
    public string? Tipo { get; set; }
    public string? Buscar { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int Pagina { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
