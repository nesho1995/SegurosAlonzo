using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ReclamosWhatsApp.Models;

public class Automatizacion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
    public string TipoEvento { get; set; } = "";
    public int? EmpresaId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public List<AutomatizacionCondicion> Condiciones { get; set; } = new();
    public List<AutomatizacionAccion> Acciones { get; set; } = new();
}

public class AutomatizacionCondicion
{
    public int Id { get; set; }
    public int AutomatizacionId { get; set; }
    public string Campo { get; set; } = "";
    public string Operador { get; set; } = "=";
    public string? Valor { get; set; }
}

public class AutomatizacionAccion
{
    public int Id { get; set; }
    public int AutomatizacionId { get; set; }
    public string TipoAccion { get; set; } = "";
    public string? ParametrosJson { get; set; }
}

public class AutomatizacionLog
{
    public int Id { get; set; }
    public int AutomatizacionId { get; set; }
    public string Automatizacion { get; set; } = "";
    public string EntidadTipo { get; set; } = "";
    public int? EntidadId { get; set; }
    public string Resultado { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public DateTime Fecha { get; set; }
}

public class AutomatizacionRequest
{
    [Required(ErrorMessage = "El nombre de la regla es requerido.")]
    public string Nombre { get; set; } = "";

    public bool Activo { get; set; } = true;

    [Required(ErrorMessage = "El evento de la regla es requerido.")]
    public string TipoEvento { get; set; } = "";

    public int? EmpresaId { get; set; }
    public List<AutomatizacionCondicionRequest> Condiciones { get; set; } = new();
    public List<AutomatizacionAccionRequest> Acciones { get; set; } = new();
}

public class AutomatizacionCondicionRequest
{
    [Required(ErrorMessage = "Selecciona el campo de la condicion.")]
    public string Campo { get; set; } = "";

    [Required(ErrorMessage = "Selecciona el operador de la condicion.")]
    public string Operador { get; set; } = "=";

    public string? Valor { get; set; }
}

public class AutomatizacionAccionRequest
{
    [Required(ErrorMessage = "Selecciona la accion.")]
    public string TipoAccion { get; set; } = "";

    public string? ParametrosJson { get; set; }
}

public class AutomatizacionTestRequest
{
    [Required(ErrorMessage = "Selecciona el evento a probar.")]
    public string TipoEvento { get; set; } = "";

    public string EntidadTipo { get; set; } = "PRUEBA";
    public int? EntidadId { get; set; }
    public Dictionary<string, object?> Datos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AutomatizacionTestResult
{
    public int ReglasEvaluadas { get; set; }
    public int ReglasCoincidentes { get; set; }
    public List<string> Mensajes { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationExecutionMode
{
    Real,
    Test
}
