using System.ComponentModel.DataAnnotations;

namespace Hagoplant.Models.ViewModels
{
    public class RegisterVm
    {
        [Required]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = default!;

        [Phone]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Required, MinLength(6)]
        public string Password { get; set; } = default!;

        [Required, Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = default!;
    }
}
