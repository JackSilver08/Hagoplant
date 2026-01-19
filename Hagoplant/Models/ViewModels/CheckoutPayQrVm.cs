namespace Hagoplant.Models.ViewModels { 
    public class CheckoutPayQrVm {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = ""; 
        public int Amount { get; set; } public string QrPayload { get; set; } = "";
        public string CheckoutUrl { get; set; } = ""; 
    }
}