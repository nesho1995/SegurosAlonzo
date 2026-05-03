using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Security;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/busqueda")]
public class BusquedaApiController : ControllerBase
{
    private readonly CarteraRepository _cartera;

    public BusquedaApiController(CarteraRepository cartera)
    {
        _cartera = cartera;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string q = "")
    {
        q = q.Trim();
        if (q.Length < 2)
            return Ok(new { clientes = Array.Empty<object>(), polizas = Array.Empty<object>() });

        var results = await _cartera.BuscarAsync(q, limit: 8);
        return Ok(results);
    }
}
