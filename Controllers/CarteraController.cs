using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers;

[Authorize(Policy = Permissions.ClientesVer)]
public class CarteraController : Controller
{
    private readonly CarteraRepository _repo;
    private readonly CarteraImportService _importService;

    public CarteraController(
        CarteraRepository repo,
        CarteraImportService importService)
    {
        _repo = repo;
        _importService = importService;
    }

    public async Task<IActionResult> Index()
    {
        var clientes = await _repo.GetClientesAsync();
        return View(clientes);
    }

    [Authorize(Policy = Permissions.ClientesCrear)]
    public IActionResult Crear()
    {
        return View(new Cliente
        {
            Activo = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ClientesCrear)]
    public async Task<IActionResult> Crear(Cliente model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var id = await _repo.InsertClienteAsync(model);

        if (!string.IsNullOrWhiteSpace(model.Telefono))
        {
            await _repo.InsertTelefonoAsync(id, NormalizarTelefono(model.Telefono), true);
        }

        if (!string.IsNullOrWhiteSpace(model.Contacto))
        {
            foreach (var tel in SepararTelefonos(model.Contacto))
            {
                await _repo.InsertTelefonoAsync(id, tel, false);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.ClientesEditar)]
    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _repo.GetClienteByIdAsync(id);

        if (cliente is null)
            return NotFound();

        ViewBag.Telefonos = await _repo.GetTelefonosClienteAsync(id);

        return View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ClientesEditar)]
    public async Task<IActionResult> Editar(Cliente model)
    {
        if (!ModelState.IsValid)
            return View(model);

        await _repo.UpdateClienteAsync(model);

        if (!string.IsNullOrWhiteSpace(model.Telefono))
        {
            await _repo.InsertTelefonoAsync(model.Id, NormalizarTelefono(model.Telefono), true);
        }

        if (!string.IsNullOrWhiteSpace(model.Contacto))
        {
            foreach (var tel in SepararTelefonos(model.Contacto))
            {
                await _repo.InsertTelefonoAsync(model.Id, tel, false);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.ClientesCrear)]
    public IActionResult Importar()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ClientesCrear)]
    public async Task<IActionResult> Importar(IFormFile archivo)
    {
        try
        {
            if (archivo == null || archivo.Length == 0)
            {
                ViewBag.Error = "Debe seleccionar un archivo Excel.";
                return View();
            }

            var extension = Path.GetExtension(archivo.FileName).ToLower();

            if (extension != ".xlsx")
            {
                ViewBag.Error = "Por ahora solo se permiten archivos .xlsx.";
                return View();
            }

            using var stream = archivo.OpenReadStream();

            var resultado = await _importService.ImportarAsync(stream);

            TempData["Mensaje"] = $"Importación completada. Clientes procesados: {resultado.clientes}. Pólizas procesadas: {resultado.polizas}.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ViewBag.Error = ex.Message;
            return View();
        }
        catch
        {
            ViewBag.Error = "No se pudo importar la cartera. Revisa el formato e intenta nuevamente.";
            return View();
        }
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var polizas = await _repo.GetPolizasByClienteAsync(id);
        var cliente = await _repo.GetClienteByIdAsync(id);

        ViewBag.Cliente = cliente;
        ViewBag.Telefonos = await _repo.GetTelefonosClienteAsync(id);

        return View(polizas);
    }

    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> EditarPoliza(int id)
    {
        var poliza = await _repo.GetPolizaByIdAsync(id);

        if (poliza is null)
            return NotFound();

        ViewBag.Cliente = await _repo.GetClienteByIdAsync(poliza.ClienteId);

        return View(poliza);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> EditarPoliza(Poliza model)
    {
        if (!ModelState.IsValid)
            return View(model);

        await _repo.UpdatePolizaAsync(model);
        TempData["Mensaje"] = "Póliza actualizada.";

        return RedirectToAction(nameof(Detalle), new { id = model.ClienteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> CambiarEstadoPoliza(int id, int clienteId, bool activo)
    {
        await _repo.SetPolizaActivaAsync(id, activo);
        TempData["Mensaje"] = activo ? "Póliza activada." : "Póliza inactivada.";

        return RedirectToAction(nameof(Detalle), new { id = clienteId });
    }

    private static IEnumerable<string> SepararTelefonos(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return Enumerable.Empty<string>();

        var partes = texto
            .Replace("/", ",")
            .Replace("-", "")
            .Replace(";", ",")
            .Split(",", StringSplitOptions.RemoveEmptyEntries);

        return partes
            .Select(x => NormalizarTelefono(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct();
    }

    private static string NormalizarTelefono(string telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return "";

        var limpio = new string(telefono.Where(char.IsDigit).ToArray());

        if (limpio.Length == 8)
            limpio = "504" + limpio;

        return limpio;
    }
}
