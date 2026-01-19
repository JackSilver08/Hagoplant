namespace Hagoplant.Models.ViewModels
{
    public class CheckoutPageVm
    {
        public CheckoutVm Form { get; set; } = new();
        public CartVm Cart { get; set; } = new();
    }
}
