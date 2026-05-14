using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers;

public class AuthController : Controller
{
    private readonly UserRepository _userRepository;
    private readonly AuditoriaService _auditoria;

    public AuthController(UserRepository userRepository, AuditoriaService auditoria)
    {
        _userRepository = userRepository;
        _auditoria = auditoria;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity!.IsAuthenticated)
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userRepository.GetUserByUsernameAsync(model.Username, includeInactive: true);

        if (user is null && model.Username == "admin")
        {
            var adminRoleId = await _userRepository.GetRoleIdByNameAsync("ADMIN") ?? 1;
            await _userRepository.CreateUserAsync("admin", BCrypt.Net.BCrypt.HashPassword("admin123"), adminRoleId);
            user = await _userRepository.GetUserByUsernameAsync(model.Username, includeInactive: true);
        }

        if (user is null)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", null, $"Usuario no encontrado: {model.Username}");
            ModelState.AddModelError(string.Empty, "Usuario o contraseña invalidos.");
            return View(model);
        }

        if (!user.IsActive)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", user.Id, "Intento de login con usuario inactivo.");
            ModelState.AddModelError(string.Empty, "El usuario esta inactivo.");
            return View(model);
        }

        if (user.LockoutUntil is not null && user.LockoutUntil > DateTime.Now)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", user.Id, "Intento de login con usuario bloqueado temporalmente.");
            ModelState.AddModelError(string.Empty, "Usuario bloqueado temporalmente por varios intentos fallidos. Intenta mas tarde.");
            return View(model);
        }

        if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            await _userRepository.RegisterFailedLoginAsync(user.Id);
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", user.Id, "Contraseña incorrecta.");
            ModelState.AddModelError(string.Empty, "Usuario o contraseña invalidos.");
            return View(model);
        }

        await _userRepository.ResetFailedLoginAsync(user.Id);
        var activeSessions = await _userRepository.CountActiveSessionsAsync(user.Id);
        if (activeSessions >= 2)
        {
            await _userRepository.RevokeOldestSessionAsync(user.Id);
            await _auditoria.RegistrarAsync("SESION_ANTIGUA_REVOCADA", "USUARIO", user.Id, "Sesion mas antigua revocada al superar limite de 2 sesiones activas.");
        }

        var sessionId = await _userRepository.CreateSessionAsync(
            user.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            DateTime.UtcNow.AddHours(8));
        var role = Permissions.NormalizeRole(user.Role?.Name);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, role),
            new("sid", sessionId),
            new("session_created_utc", DateTime.UtcNow.ToString("O"))
        };
        claims.AddRange(Permissions.ForRole(role).Select(permission => new Claim("perm", permission)));

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        await _auditoria.RegistrarAsync("LOGIN_EXITOSO", "USUARIO", user.Id, "Inicio de sesion correcto.");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
        await _auditoria.RegistrarAsync("LOGOUT", "USUARIO", userId, "Cierre de sesion.");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    [Authorize]
    public IActionResult CambiarPassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarPassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userRepository.GetUserByUsernameAsync(User.Identity?.Name ?? "");
        if (user is null || !BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "La contraseña actual no es correcta.");
            return View(model);
        }

        await _userRepository.ChangePasswordAsync(user.Id, BCrypt.Net.BCrypt.HashPassword(model.NewPassword));
        await _auditoria.RegistrarAsync("CAMBIO_PASSWORD", "USUARIO", user.Id, "Cambio de contraseña.");
        TempData["Mensaje"] = "Contraseña actualizada.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        _ = _auditoria.RegistrarAsync("ACCESO_DENEGADO", "SEGURIDAD", null, "Acceso denegado por permisos.");
        return View();
    }
}
