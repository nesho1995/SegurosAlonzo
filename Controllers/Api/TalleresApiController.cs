using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/talleres")]
public class TalleresApiController : ControllerBase
{
    private const long MaxExcelBytes = 5 * 1024 * 1024;
    private readonly TallerRepository _talleres;
    private readonly TallerImportService _import;

    public TalleresApiController(TallerRepository talleres, TallerImportService import)
    {
        _talleres = talleres;
        _import = import;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.TalleresVer)]
    public async Task<IActionResult> Get(
        string? buscar = null,
        string? estado = "ACTIVO",
        string? ciudad = null,
        string? aseguradora = null)
    {
        return Ok(new { items = await _talleres.GetAsync(buscar, estado, ciudad, aseguradora) });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public async Task<IActionResult> Create(Taller taller)
    {
        var error = Validar(taller);
        if (error is not null)
            return BadRequest(error);

        var id = await _talleres.InsertAsync(taller);
        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public async Task<IActionResult> Update(int id, Taller taller)
    {
        if (id != taller.Id)
            return BadRequest("El taller no coincide con la ruta.");

        var error = Validar(taller);
        if (error is not null)
            return BadRequest(error);

        await _talleres.UpdateAsync(taller);
        return NoContent();
    }

    [HttpPatch("{id:int}/estado")]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] TallerEstadoRequest request)
    {
        await _talleres.SetActivoAsync(id, request.Activo);
        return NoContent();
    }

    [HttpGet("detectados")]
    [Authorize(Policy = Permissions.TalleresVer)]
    public async Task<IActionResult> Detectados(string estado = "PENDIENTE")
    {
        return Ok(new { items = await _talleres.GetDetectadosAsync(estado) });
    }

    [HttpPost("detectados/{id:int}/aprobar")]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public async Task<IActionResult> Aprobar(int id)
    {
        await _talleres.AprobarDetectadoAsync(id);
        return NoContent();
    }

    [HttpPost("detectados/{id:int}/descartar")]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public async Task<IActionResult> Descartar(int id)
    {
        await _talleres.DescartarDetectadoAsync(id);
        return NoContent();
    }

    [HttpGet("plantilla")]
    [Authorize(Policy = Permissions.TalleresVer)]
    public IActionResult Plantilla()
    {
        return File(
            _import.CrearPlantilla(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_talleres.xlsx");
    }

    [HttpGet("plantilla-csv")]
    [Authorize(Policy = Permissions.TalleresVer)]
    public IActionResult PlantillaCsv()
    {
        return File(_import.CrearPlantillaCsv(), "text/csv; charset=utf-8", "plantilla_talleres.csv");
    }

    [HttpPost("preview")]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public IActionResult Preview(IFormFile archivo)
    {
        var error = ValidarArchivo(archivo);
        if (error is not null)
            return BadRequest(error);

        try
        {
            using var stream = archivo.OpenReadStream();
            return Ok(new { items = _import.Preview(stream, archivo.FileName) });
        }
        catch
        {
            return BadRequest("No se pudo leer el archivo. Descarga la plantilla y revisa que las columnas no hayan sido cambiadas.");
        }
    }

    [HttpPost("importar")]
    [Authorize(Policy = Permissions.TalleresEditar)]
    public async Task<IActionResult> Importar(IFormFile archivo)
    {
        var error = ValidarArchivo(archivo);
        if (error is not null)
            return BadRequest(error);

        List<TallerImportPreview> preview;
        try
        {
            using var stream = archivo.OpenReadStream();
            preview = _import.Preview(stream, archivo.FileName);
        }
        catch
        {
            return BadRequest("No se pudo leer el archivo. Descarga la plantilla y revisa que las columnas no hayan sido cambiadas.");
        }

        var validos = preview.Where(x => x.Errores.Count == 0).ToList();

        foreach (var item in validos)
            await _talleres.UpsertAsync(item.Taller);

        return Ok(new
        {
            importados = validos.Count,
            rechazados = preview.Count - validos.Count,
            errores = preview.Where(x => x.Errores.Count > 0)
        });
    }

    private static string? Validar(Taller taller)
    {
        if (string.IsNullOrWhiteSpace(taller.Nombre))
            return "El nombre del taller es requerido.";
        if (string.IsNullOrWhiteSpace(taller.Ciudad))
            return "La ciudad es requerida.";
        if (taller.AseguradorasAceptadas.Count == 0 && string.IsNullOrWhiteSpace(taller.Aseguradora))
            return "Agrega al menos una aseguradora.";
        if (taller.RamosAtendidos.Count == 0 && string.IsNullOrWhiteSpace(taller.Ramo))
            return "Agrega al menos un ramo.";

        return null;
    }

    private static string? ValidarArchivo(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return "Selecciona un archivo CSV o Excel.";

        if (archivo.Length > MaxExcelBytes)
            return "El archivo supera el limite permitido de 5 MB.";

        var extension = Path.GetExtension(archivo.FileName);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return "Solo se permite formato .csv o .xlsx.";
        }

        return null;
    }
}

public sealed class TallerEstadoRequest
{
    public bool Activo { get; set; }
}
