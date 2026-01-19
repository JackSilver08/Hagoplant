using System.ComponentModel.DataAnnotations;

namespace Hagoplant.Models.ViewModels
{
    public class CheckoutVm
    {
        [Required] public string CustomerName { get; set; } = "";
        [Required] public string Phone { get; set; } = "";
        public string? Email { get; set; }

        public ShippingAddressVm ShippingAddress { get; set; } = new();
    }

    public class ShippingAddressVm
    {
        public string? AddressLine { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? Province { get; set; }
        public string? Note { get; set; }
    }
}
