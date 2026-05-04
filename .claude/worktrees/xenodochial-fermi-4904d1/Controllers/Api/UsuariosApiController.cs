using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.UsuariosAdministrar)]
[Route("api/usuarios")]
public class UsuariosApiController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly AuditoriaService _auditoria;

    public UsuariosApiController(UserRepository users, AuditoriaService auditoria)
    {
        _users = users;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var usuarios = await _users.GetUsersAsync();
        var roles = await _users.GetRolesAsync();
        return Ok(new
        {
            usuarios = usuarios.Select(user => new
            {
                user.Id,
                user.Username,
                user.RoleId,
                user.RoleName,
                user.IsActive,
                customPermissions = ParseCustomPermissions(user)
            }),
            roles,
            permisosDisponibles = Permissions.All.OrderBy(x => x).ToArray()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUsuarioRequest request)
    {
        var username = request.Username.Trim();
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { error = "El usuario es requerido." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { error = "La contrasena debe tener al menos 8 caracteres." });

        if (!await _users.RoleExistsAsync(request.RoleId))
            return BadRequest(new { error = "Selecciona un rol valido." });

        if (await _users.GetUserByUsernameAsync(username, includeInactive: true) is not null)
            return Conflict(new { error = "Ya existe un usuario con ese nombre." });

        var id = await _users.CreateUserAsync(username, BCrypt.Net.BCrypt.HashPassword(request.Password), request.RoleId);
        await _auditoria.RegistrarAsync("CREAR_USUARIO", "USUARIO", id, $"Usuario creado: {username}");

        return Ok(new { id, message = "Usuario creado." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUsuarioRequest request)
    {
        if (!await _users.RoleExistsAsync(request.RoleId))
            return BadRequest(new { error = "Selecciona un rol valido." });

        await _users.UpdateUserAsync(id, request.RoleId, request.IsActive);
        await _auditoria.RegistrarAsync(
            request.IsActive ? "ACTIVAR_USUARIO" : "INACTIVAR_USUARIO",
            "USUARIO",
            id,
            request.IsActive ? "Usuario activado." : "Usuario inactivado.");

        return Ok(new { message = "Usuario actualizado." });
    }

    [HttpPost("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "La nueva contrasena debe tener al menos 8 caracteres." });

        await _users.ChangePasswordAsync(id, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        await _auditoria.RegistrarAsync("CAMBIO_PASSWORD", "USUARIO", id, "Contrasena actualizada por administrador.");
        return Ok(new { message = "Contrasena actualizada." });
    }

    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int id, UpdatePermissionsRequest request)
    {
        await _users.UpdateUserPermissionsAsync(id, request.Permissions ?? Array.Empty<string>());
        await _auditoria.RegistrarAsync("EDITAR_PERMISOS_USUARIO", "USUARIO", id, "Permisos personalizados actualizados.");
        return Ok(new { message = "Permisos actualizados." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : 0;
        if (id == currentUserId)
            return BadRequest(new { error = "No puedes eliminar tu propio usuario." });

        await _users.DeleteUserAsync(id);
        await _auditoria.RegistrarAsync("ELIMINAR_USUARIO", "USUARIO", id, "Usuario eliminado por administrador.");
        return NoContent();
    }

    private static string[] ParseCustomPermissions(UserAdminViewModel user)
    {
        if (string.IsNullOrWhiteSpace(user.CustomPermissionsJson))
            return Array.Empty<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(user.CustomPermissionsJson) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed record CreateUsuarioRequest(string Username, string Password, int RoleId);
public sealed record UpdateUsuarioRequest(int RoleId, bool IsActive);
public sealed record ResetPasswordRequest(string NewPassword);
public sealed record UpdatePermissionsRequest(string[] Permissions);
