using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers;

[Authorize(Policy = Permissions.UsuariosAdministrar)]
public class UsuariosController : Controller
{
    private readonly UserRepository _users;
    private readonly AuditoriaService _auditoria;

    public UsuariosController(UserRepository users, AuditoriaService auditoria)
    {
        _users = users;
        _auditoria = auditoria;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Roles = await _users.GetRolesAsync();
        ViewBag.NuevoUsuario = new CreateUserViewModel { RoleId = 2 };

        var usuarios = await _users.GetUsersAsync();
        return View(usuarios);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revisa usuario, contraseña y rol.";
            return RedirectToAction(nameof(Index));
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);
        await _users.CreateUserAsync(model.Username.Trim(), hash, model.RoleId);
        await _auditoria.RegistrarAsync("CREAR_USUARIO", "USUARIO", null, $"Usuario creado: {model.Username.Trim()}");

        TempData["Mensaje"] = "Usuario creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(int id, int roleId, bool isActive)
    {
        await _users.UpdateUserAsync(id, roleId, isActive);
        await _auditoria.RegistrarAsync(isActive ? "ACTIVAR_USUARIO" : "INACTIVAR_USUARIO", "USUARIO", id, isActive ? "Usuario activado." : "Usuario inactivado.");
        TempData["Mensaje"] = "Usuario actualizado.";

        return RedirectToAction(nameof(Index));
    }
}
