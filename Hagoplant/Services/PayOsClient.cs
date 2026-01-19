using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Hagoplant.Models.ViewModels;

namespace Hagoplant.Services
{
    public class PayOsClient
    {
        private readonly HttpClient _http;
        private readonly PayOsOptions _opt;

        public PayOsClient(HttpClient http, IOptions<PayOsOptions> opt)
        {
            _http = http;
            _opt = opt.Value;
        }

        public async Task<PayOsCreateResp> CreatePaymentAsync(
            int orderCode,
            int amount,
            string description,
            string cancelUrl,
            string returnUrl)
        {
            var dataToSign =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

            var signature = HmacSha256Hex(dataToSign, _opt.ChecksumKey);

            var payload = new PayOsCreateReq(orderCode, amount, description, cancelUrl, returnUrl, signature);

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_opt.BaseUrl}/v2/payment-requests");
            req.Headers.Add("x-client-id", _opt.ClientId);
            req.Headers.Add("x-api-key", _opt.ApiKey);
            req.Content = JsonContent.Create(payload);

            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var obj = await res.Content.ReadFromJsonAsync<PayOsCreateResp>();
            return obj ?? throw new InvalidOperationException("payOS empty response");
        }

        private static string HmacSha256Hex(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
