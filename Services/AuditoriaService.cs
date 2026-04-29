using System.Security.Claims;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class AuditoriaService
{
    private readonly AuditoriaRepository _repo;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditoriaService(AuditoriaRepository repo, IHttpContextAccessor httpContextAccessor)
    {
        _repo = repo;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RegistrarAsync(string accion, string entidadTipo, int? entidadId, string descripcion)
    {
        var context = _httpContextAccessor.HttpContext;
        var userIdText = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdText, out var parsed) ? parsed : null;

        await _repo.InsertAsync(new AuditoriaLog
        {
            UsuarioId = userId,
            Accion = accion,
            EntidadTipo = NormalizarEntidad(entidadTipo),
            EntidadId = entidadId,
            Descripcion = descripcion.Length > 1000 ? descripcion[..1000] : descripcion,
            Ip = context?.Connection.RemoteIpAddress?.ToString()
        });
    }

    private static string NormalizarEntidad(string value)
    {
        return (value ?? "").Trim().ToUpperInvariant();
    }
}
