using System.ComponentModel.DataAnnotations;

namespace ReclamosWhatsApp.Models;

public class UserAdminViewModel
{
    public int Id { get; set; }

    [Required]
    public string Username { get; set; } = "";

    [Required]
    public int RoleId { get; set; }

    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? CustomPermissionsJson { get; set; }
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "El usuario es requerido")]
    [StringLength(50)]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es requerida")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required]
    public int RoleId { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "La contraseña actual es requerida")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Confirma la nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
