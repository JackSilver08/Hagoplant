using System.ComponentModel.DataAnnotations;

namespace Hagoplant.ViewModels
{
    public class ProfileUpdateVm
    {
        [Display(Name = "Email")]
        public string Email { get; set; } = default!; // hiển thị readonly

        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [Display(Name = "Ngày tạo")]
        public string? CreatedAtText { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; }
    }
}
