namespace Hagoplant.Models.ViewModels
{
    public record PayOsCreateReq(
        int orderCode,
        int amount,
        string description,
        string cancelUrl,
        string returnUrl,
        string signature
    );

    public class PayOsCreateResp
    {
        public string code { get; set; } = "";
        public string desc { get; set; } = "";
        public PayOsCreateData data { get; set; } = new();
        public string signature { get; set; } = "";
    }

    public class PayOsCreateData
    {
        public string paymentLinkId { get; set; } = "";
        public int orderCode { get; set; }
        public int amount { get; set; }
        public string status { get; set; } = "";
        public string checkoutUrl { get; set; } = "";
        public string qrCode { get; set; } = "";
    }
    
    public class PayOsGetPaymentResp
    {
        public string code { get; set; } = "";
        public string desc { get; set; } = "";
        public PayOsGetPaymentData? data { get; set; }
    }

    public class PayOsGetPaymentData
    {
        public string id { get; set; } = "";
        public int orderCode { get; set; }
        public int amount { get; set; }
        public int amountPaid { get; set; }
        public int amountRemaining { get; set; }
        public string status { get; set; } = "";
        public string createdAt { get; set; } = "";
    }
}
