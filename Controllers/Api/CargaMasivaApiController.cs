using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.ClientesCrear)]
[Route("api/carga-masiva")]
[EnableRateLimiting("upload")]
public class CargaMasivaApiController : ControllerBase
{
    private readonly PlantillaCargaService _plantillas;
    private readonly CarteraImportService _carteraImport;
    private readonly ReclamoHistoricoImportService _reclamosImport;
    private readonly AuditoriaService _auditoria;

    public CargaMasivaApiController(
        PlantillaCargaService plantillas,
        CarteraImportService carteraImport,
        ReclamoHistoricoImportService reclamosImport,
        AuditoriaService auditoria)
    {
        _plantillas = plantillas;
        _carteraImport = carteraImport;
        _reclamosImport = reclamosImport;
        _auditoria = auditoria;
    }

    [HttpGet("plantilla-cartera")]
    public IActionResult PlantillaCartera()
    {
        return File(
            _plantillas.CrearPlantillaCartera(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_cartera.xlsx");
    }

    [HttpGet("plantilla-cartera-financiera")]
    public IActionResult PlantillaCarteraFinanciera()
    {
        return File(
            _plantillas.CrearPlantillaCarteraFinanciera(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_cartera_financiera.xlsx");
    }

    [HttpPost("cartera/preview")]
    public async Task<IActionResult> PreviewCartera([FromForm] IFormFile archivo)
    {
        var validation = ValidateExcel(archivo);
        if (validation is not null)
            return BadRequest(new { error = validation });

        using var stream = archivo.OpenReadStream();
        var preview = _carteraImport.Preview(stream);
        await _auditoria.RegistrarAsync(
            "PREVIEW_CARTERA_HISTORICA",
            "CARGA_MASIVA",
            null,
            $"Preview generado para {archivo.FileName}: filas {preview.TotalRows}, errores {preview.ErrorCount}, advertencias {preview.WarningCount}.");
        return Ok(preview);
    }

    [HttpPost("cartera/importar")]
    public async Task<IActionResult> ImportarCartera([FromForm] IFormFile archivo)
    {
        var validation = ValidateExcel(archivo);
        if (validation is not null)
            return BadRequest(new { error = validation });

        using var previewStream = archivo.OpenReadStream();
        var preview = _carteraImport.Preview(previewStream);
        var result = await _carteraImport.ImportarPreviewDetalladoAsync(preview.Rows);
        await _auditoria.RegistrarAsync(
            preview.HasCriticalErrors ? "IMPORTACION_PARCIAL_CARTERA" : "IMPORTACION_MASIVA_CARTERA",
            "CARGA_MASIVA",
            null,
            $"Archivo {archivo.FileName}: filas importadas {result.FilasImportadas}, filas rechazadas {result.FilasRechazadas}, clientes {result.Clientes}, polizas {result.Polizas}, polizas duplicadas {result.PolizasDuplicadas}, advertencias {preview.WarningCount}.");

        return Ok(new
        {
            clientes = result.Clientes,
            polizas = result.Polizas,
            polizasDuplicadas = result.PolizasDuplicadas,
            filasImportadas = result.FilasImportadas,
            filasRechazadas = result.FilasRechazadas,
            advertencias = preview.WarningCount
        });
    }

    [HttpPost("cartera/reporte")]
    public async Task<IActionResult> ReporteCartera([FromForm] IFormFile archivo)
    {
        var validation = ValidateExcel(archivo);
        if (validation is not null)
            return BadRequest(new { error = validation });

        using var stream = archivo.OpenReadStream();
        var preview = _carteraImport.Preview(stream);
        await _auditoria.RegistrarAsync("DESCARGAR_REPORTE_CARGA_CARTERA", "CARGA_MASIVA", null, $"Reporte descargado para {archivo.FileName}: errores {preview.ErrorCount}, advertencias {preview.WarningCount}.");
        return File(
            _carteraImport.CrearReporteErrores(preview),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "reporte_carga_cartera.xlsx");
    }

    [HttpPost("cartera/excel-limpio")]
    public async Task<IActionResult> ExcelLimpioCartera([FromForm] IFormFile archivo)
    {
        var validation = ValidateExcel(archivo);
        if (validation is not null)
            return BadRequest(new { error = validation });

        using var stream = archivo.OpenReadStream();
        var preview = _carteraImport.Preview(stream);
        await _auditoria.RegistrarAsync("DESCARGAR_EXCEL_LIMPIO_CARTERA", "CARGA_MASIVA", null, $"Excel limpio descargado para {archivo.FileName}: importables {preview.Rows.Count(x => x.Errors.Count == 0)}, rechazadas {preview.Rows.Count(x => x.Errors.Count > 0)}.");
        return File(
            _carteraImport.CrearExcelLimpio(preview),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "cartera_limpia.xlsx");
    }

    [HttpPost("reclamos/preview")]
    public async Task<IActionResult> PreviewReclamos([FromForm] IFormFile archivo)
    {
        var validation = ValidateExcel(archivo);
        if (validation is not null)
            return BadRequest(new { error = validation });

        using var stream = archivo.OpenReadStream();
        var preview = await _reclamosImport.PreviewAsync(stream);
        await _auditoria.RegistrarAsync(
            "PREVIEW_RECLAMOS_HISTORICOS",
            "CARGA_MASIVA",
            null,
            $"Preview reclamos historicos {archivo.FileName}: filas {preview.TotalRows}, importables {preview.ImportableCount}, duplicados {preview.DuplicateCount}, errores {preview.ErrorCount}.");
        return Ok(preview);
    }

    [HttpPost("reclamos/importar")]
    public async Task<IActionResult> ImportarReclamos([FromForm] IFormFile archivo)
    {
        var validation = ValidateExcel(archivo);
        if (validation is not null)
            return BadRequest(new { error = validation });

        using var stream = archivo.OpenReadStream();
        var result = await _reclamosImport.ImportAsync(stream);
        await _auditoria.RegistrarAsync(
            "IMPORTAR_RECLAMOS_HISTORICOS",
            "CARGA_MASIVA",
            null,
            $"Reclamos historicos {archivo.FileName}: importados {result.Importados}, duplicados {result.Duplicados}, rechazados {result.Rechazados}.");
        return Ok(result);
    }

    private static string? ValidateExcel(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return "Selecciona un archivo Excel.";

        if (Path.GetExtension(archivo.FileName).ToLowerInvariant() != ".xlsx")
            return "Solo se permiten archivos .xlsx.";

        if (archivo.Length > 5 * 1024 * 1024)
            return "El archivo supera el tamano maximo de 5 MB.";

        return null;
    }
}
