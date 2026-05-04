namespace ReclamosWhatsApp.Models;

public class ExtractorAdvancedConfig
{
    public string RemitentesPermitidos { get; set; } = "";
    public string PalabrasClaveAsunto { get; set; } = "reclamo,siniestro";
    public string AseguradorasReglas { get; set; } = "";
    public string CamposObligatorios { get; set; } = "Asegurado,Poliza,Placa,Reclamo,Conductor,Celular";
    public string PlantillaWhatsApp { get; set; } =
        "Estimado {Conductor}, hemos recibido el reclamo {Reclamo} de la poliza {Poliza}. Le estaremos dando seguimiento.";
}

public class ExtractorTestRequest
{
    public string Remitente { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Cuerpo { get; set; } = "";
}

public class ExtractorTestResult
{
    public string TextoOriginal { get; set; } = "";
    public Dictionary<string, string> CamposDetectados { get; set; } = new();
    public List<string> CamposFaltantes { get; set; } = new();
    public int Confianza { get; set; }
    public string Mensaje { get; set; } = "";
    public List<TallerSugerido> TalleresSugeridos { get; set; } = new();
    public TallerDetectado? TallerDetectado { get; set; }
}

public class Taller
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string? Zona { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public string? Contacto { get; set; }
    public string Aseguradora { get; set; } = "";
    public string? Ramo { get; set; }
    public bool Activo { get; set; } = true;
    public bool EsPreferido { get; set; }
    public int OrdenPrioridad { get; set; } = 100;
    public string? Observaciones { get; set; }
    public List<string> AseguradorasAceptadas { get; set; } = new();
    public List<string> RamosAtendidos { get; set; } = new();
    public DateTime FechaCreacion { get; set; }
}

public class TallerAseguradora
{
    public int Id { get; set; }
    public int TallerId { get; set; }
    public string Aseguradora { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public class TallerRamo
{
    public int Id { get; set; }
    public int TallerId { get; set; }
    public string RamoNormalizado { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public class TallerDetectado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Ciudad { get; set; }
    public string? Aseguradora { get; set; }
    public string? Ramo { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string TextoOrigen { get; set; } = "";
    public string Estado { get; set; } = "PENDIENTE";
    public DateTime FechaCreacion { get; set; }
}

public class TallerSugerido
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Aseguradora { get; set; } = "";
    public string? Ramo { get; set; }
    public string? Telefono { get; set; }
    public string? WhatsApp { get; set; }
    public string? Direccion { get; set; }
    public bool EsPreferido { get; set; }
    public int OrdenPrioridad { get; set; }
    public string Criterio { get; set; } = "";
}

public class TallerImportPreview
{
    public int Fila { get; set; }
    public Taller Taller { get; set; } = new();
    public List<string> Errores { get; set; } = new();
}
