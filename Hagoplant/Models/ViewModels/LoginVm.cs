using System.ComponentModel.DataAnnotations;

namespace Hagoplant.Models.ViewModels
{
    public class LoginVm
    {
        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Required]
        public string Password { get; set; } = default!;

        public bool RememberMe { get; set; }
    }
}
