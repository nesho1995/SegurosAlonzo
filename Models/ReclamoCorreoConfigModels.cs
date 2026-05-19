namespace ReclamosWhatsApp.Models;

public class ReclamoCorreoConfig
{
    public bool EmailEnabled { get; set; }
    public bool WorkerEnabled { get; set; }
    public string Mailbox { get; set; } = "INBOX";
    public bool MarkAsRead { get; set; }
    public int LookbackHours { get; set; } = 24;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ReclamoCorreoConfigDto
{
    public bool EmailEnabled { get; set; }
    public bool WorkerEnabled { get; set; }
    public string Mailbox { get; set; } = "INBOX";
    public bool MarkAsRead { get; set; }
    public int LookbackHours { get; set; } = 24;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string PasswordMasked { get; set; } = "";
}

public class SmtpConfig
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Reclamos";
    public string InternalCopyEmails { get; set; } = "";
}

public class SmtpConfigDto
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string Username { get; set; } = "";
    public string PasswordMasked { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Reclamos";
    public string InternalCopyEmails { get; set; } = "";
}

public class ReclamoWorkerEstado
{
    public DateTime? UltimaEjecucionUtc { get; set; }
    public string? UltimoError { get; set; }
    public int CorreosEncontrados { get; set; }
    public int ReclamosValidos { get; set; }
    public int CorreosProcesados { get; set; }
    public int CorreosIgnorados { get; set; }
    public int CorreosDuplicados { get; set; }
    public int CorreosConError { get; set; }
    public List<CorreoProcesamientoDetalle> Detalles { get; set; } = new();
}

public class CorreoProcesamientoDetalle
{
    public string Subject { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int? ReclamoId { get; set; }
}

public class CorreoRevisionItem
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int? ReclamoId { get; set; }
    public string BodyPreview { get; set; } = "";
    public DateTime FechaProcesamientoUtc { get; set; }
}

public class CorreoReclamoPatron
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
    public int Prioridad { get; set; } = 100;
    public string CampoDestino { get; set; } = "";
    public string Fuente { get; set; } = "SUBJECT_BODY";
    public string TipoRegla { get; set; } = "REGEX";
    public string Patron { get; set; } = "";
    public string? GrupoRegex { get; set; }
    public bool Requerido { get; set; }
    public bool NormalizarTexto { get; set; } = true;
    public string? Descripcion { get; set; }
    public string? EjemploEntrada { get; set; }
    public string? EjemploSalidaEsperada { get; set; }
}

public class CorreoReclamoPlantilla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activa { get; set; } = true;
    public int Prioridad { get; set; } = 100;
    public string? Descripcion { get; set; }
}

public class CorreoReclamoPlantillaRegla
{
    public int PlantillaId { get; set; }
    public int PatronId { get; set; }
}

public class CorreoReclamoCondicion
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public string Fuente { get; set; } = "SUBJECT_BODY";
    public string TipoRegla { get; set; } = "CONTIENE";
    public string Patron { get; set; } = "";
    public string OperadorGrupo { get; set; } = "AND";
    public int GrupoCondicion { get; set; } = 1;
}

public class ProbarPatronesRequest
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int? PlantillaId { get; set; }
}

public class ProbarPatronesResult
{
    public int? PlantillaId { get; set; }
    public string PlantillaNombre { get; set; } = "";
    public bool PlantillaCumple { get; set; }
    public Dictionary<string, string> CamposDetectados { get; set; } = new();
    public List<string> CamposFaltantes { get; set; } = new();
    public Dictionary<string, string> ReglaQueDetectoPorCampo { get; set; } = new();
}
