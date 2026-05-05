using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly AuditoriaService _auditoria;

    public AuthApiController(UserRepository users, AuditoriaService auditoria)
    {
        _users = users;
        _auditoria = auditoria;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var username = request.Username.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Ingresa usuario y contrasena." });

        var user = await _users.GetUserByUsernameAsync(username, includeInactive: true);
        if (user is null && username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            var adminRoleId = await _users.EnsureRoleAsync("ADMIN");
            await _users.CreateUserAsync("admin", BCrypt.Net.BCrypt.HashPassword("admin123"), adminRoleId);
            user = await _users.GetUserByUsernameAsync(username, includeInactive: true);
        }

        if (user is null)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", null, $"Usuario no encontrado: {username}");
            return Unauthorized(new { error = "Usuario o contrasena invalidos." });
        }

        if (!user.IsActive)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", user.Id, "Intento de login con usuario inactivo.");
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "El usuario esta inactivo." });
        }

        if (user.LockoutUntil is not null && user.LockoutUntil > DateTime.Now)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", user.Id, "Intento de login con usuario bloqueado temporalmente.");
            return StatusCode(StatusCodes.Status423Locked, new { error = "Usuario bloqueado temporalmente por varios intentos fallidos. Intenta mas tarde." });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _users.RegisterFailedLoginAsync(user.Id);
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", "USUARIO", user.Id, "Contrasena incorrecta.");
            return Unauthorized(new { error = "Usuario o contrasena invalidos." });
        }

        await _users.ResetFailedLoginAsync(user.Id);
        var activeSessions = await _users.CountActiveSessionsAsync(user.Id);
        if (activeSessions >= 2)
        {
            // Revocar la sesión más antigua para dar paso a la nueva
            await _users.RevokeOldestSessionAsync(user.Id);
            await _auditoria.RegistrarAsync("SESION_ANTIGUA_REVOCADA", "USUARIO", user.Id, "Sesion mas antigua revocada al superar limite de 2 sesiones activas.");
        }

        var sessionId = await _users.CreateSessionAsync(
            user.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            DateTime.UtcNow.AddHours(8));
        await SignInAsync(user, sessionId);
        await _auditoria.RegistrarAsync("LOGIN_EXITOSO", "USUARIO", user.Id, "Inicio de sesion correcto.");

        return Ok(ToSession(user));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            username = User.Identity?.Name ?? "",
            roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray(),
            permisos = User.FindAll("perm").Select(x => x.Value).Distinct().OrderBy(x => x).ToArray()
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
        var sessionId = User.FindFirstValue("sid");
        if (userId.HasValue && !string.IsNullOrWhiteSpace(sessionId))
            await _users.RevokeSessionAsync(userId.Value, sessionId);
        await _auditoria.RegistrarAsync("LOGOUT", "USUARIO", userId, "Cierre de sesion.");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Sesion cerrada." });
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<IActionResult> CambiarPassword(ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Completa la contrasena actual y la nueva contrasena." });

        if (request.NewPassword.Length < 8)
            return BadRequest(new { error = "La nueva contrasena debe tener al menos 8 caracteres." });

        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { error = "Las contrasenas no coinciden." });

        var user = await _users.GetUserByUsernameAsync(User.Identity?.Name ?? "");
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { error = "La contrasena actual no es correcta." });

        await _users.ChangePasswordAsync(user.Id, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        await _users.RevokeOtherSessionsAsync(user.Id, User.FindFirstValue("sid"));
        await _auditoria.RegistrarAsync("CAMBIO_PASSWORD", "USUARIO", user.Id, "Cambio de contrasena.");
        return Ok(new { message = "Contrasena actualizada." });
    }

    private async Task SignInAsync(Models.User user, string sessionId)
    {
        var role = Permissions.NormalizeRole(user.Role?.Name);
        var effectivePermissions = ResolvePermissions(user, role);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, role),
            new("sid", sessionId)
        };
        claims.AddRange(effectivePermissions.Select(permission => new Claim("perm", permission)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private static object ToSession(Models.User user)
    {
        var role = Permissions.NormalizeRole(user.Role?.Name);
        var effectivePermissions = ResolvePermissions(user, role);
        return new
        {
            usuarioId = user.Id.ToString(),
            username = user.Username,
            roles = new[] { role },
            permisos = effectivePermissions.OrderBy(x => x).ToArray()
        };
    }

    private static IReadOnlyCollection<string> ResolvePermissions(Models.User user, string role)
    {
        var fallback = Permissions.ForRole(role);
        if (string.IsNullOrWhiteSpace(user.CustomPermissionsJson))
            return fallback;

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(user.CustomPermissionsJson) ?? Array.Empty<string>();
            var valid = parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => Permissions.All.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return valid.Length == 0 ? fallback : valid;
        }
        catch
        {
            return fallback;
        }
    }
}

public sealed record LoginRequest(string Username, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
