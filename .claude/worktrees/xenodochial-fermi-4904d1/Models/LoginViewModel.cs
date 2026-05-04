using System.ComponentModel.DataAnnotations;

namespace ReclamosWhatsApp.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El usuario es requerido")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
