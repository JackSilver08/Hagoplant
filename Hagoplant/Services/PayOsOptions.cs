namespace Hagoplant.Services
{
    public class PayOsOptions
    {
        public string ClientId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ChecksumKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    }
}
