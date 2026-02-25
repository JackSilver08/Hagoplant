using Hagoplant.DBcontext;
using Hagoplant.Models;
using Hagoplant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hagoplant.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly HagoDbContext _db;
        private readonly IAuditService _audit;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentsController> _logger;
        private readonly PayOsClient _payOs;

        public PaymentsController(
            HagoDbContext db,
            IAuditService audit,
            IConfiguration config,
            ILogger<PaymentsController> logger,
            PayOsClient payOs)
        {
            _db = db;
            _audit = audit;
            _config = config;
            _logger = logger;
            _payOs = payOs;
        }

        /// <summary>
        /// PayOS redirect sau khi thanh toán thành công
        /// ⚠ Đây chỉ là redirect URL, KHÔNG dùng để cập nhật trạng thái payment
        /// Trạng thái thực sự được cập nhật qua Webhook
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PayReturn(Guid orderId)
        {
            var order = await _db.Orders.Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound();

            // Nếu đang PENDING, thử hỏi PayOS
            if (order.PaymentStatus == PaymentStatuses.Pending || order.PaymentStatus == PaymentStatuses.Unpaid)
            {
                var payment = order.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                if (payment != null && payment.Status == PaymentStatuses.Pending && payment.Provider == "payos" && !string.IsNullOrEmpty(payment.TransactionRef))
                {
                    try
                    {
                        var payOsStatus = await _payOs.GetPaymentAsync(payment.TransactionRef);
                        if (payOsStatus?.data != null && payOsStatus.data.status == "PAID")
                        {
                            payment.Status = PaymentStatuses.Paid;
                            payment.PaidAt = DateTimeOffset.UtcNow;
                            
                            order.PaymentStatus = PaymentStatuses.Paid;
                            order.Status = OrderStatuses.Paid;
                            order.ConfirmedAt = DateTimeOffset.UtcNow;
                            order.UpdatedAt = DateTimeOffset.UtcNow;
                            
                            await _db.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi kiểm tra trạng thái PayOS trong PayReturn");
                    }
                }
            }

            TempData["Toast.Ok"] = "1";
            TempData["Toast.Message"] = "Cảm ơn bạn đã đặt hàng! Chúng tôi sẽ xác nhận đơn hàng của bạn trong thời gian sớm nhất.";

            return RedirectToAction("History", "Order");
        }

        /// <summary>
        /// PayOS redirect khi người dùng hủy thanh toán
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PayCancel(Guid orderId)
        {
            var order = await _db.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null && order.Status == OrderStatuses.AwaitingPayment)
            {
                order.Status = OrderStatuses.Cancelled;
                order.CancelledAt = DateTimeOffset.UtcNow;
                order.CancelReason = "Người dùng hủy trong quá trình thanh toán";
                order.UpdatedAt = DateTimeOffset.UtcNow;

                // Cập nhật payment status
                var latestPayment = order.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                if (latestPayment != null)
                {
                    latestPayment.Status = PaymentStatuses.Cancelled;
                }

                await _db.SaveChangesAsync();

                await _audit.LogAsync(
                    AuditActions.OrderCancelled,
                    entityType: "Order",
                    entityId: orderId.ToString(),
                    details: "Order cancelled by user during payment",
                    result: AuditResult.Success);
            }

            TempData["Toast.Ok"] = "0";
            TempData["Toast.Message"] = "Bạn đã hủy thanh toán. Đơn hàng đã bị hủy.";
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// PayOS Webhook - xử lý thông báo thanh toán từ PayOS server
        /// ⚠ QUAN TRỌNG: Phải xác thực chữ ký webhook trước khi xử lý
        /// </summary>
        [HttpPost("/payments/webhook/payos")]
        [IgnoreAntiforgeryToken] // Webhook không có CSRF token
        public async Task<IActionResult> PayOsWebhook()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            // 1. Xác thực chữ ký webhook
            var checksumKey = _config["PayOs:ChecksumKey"];
            var payosSignature = Request.Headers["x-payos-signature"].FirstOrDefault();

            if (string.IsNullOrEmpty(checksumKey))
            {
                _logger.LogError("PayOS ChecksumKey not configured!");
                await _audit.LogAsync(
                    AuditActions.PaymentWebhookFailed,
                    details: "ChecksumKey not configured",
                    result: AuditResult.Failure);
                return StatusCode(500);
            }

            // Tính HMAC-SHA256 của raw body
            var expectedSignature = ComputeHmacSha256(rawBody, checksumKey);

            if (string.IsNullOrEmpty(payosSignature) ||
                !string.Equals(expectedSignature, payosSignature, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PayOS webhook signature mismatch. Expected: {Expected}, Got: {Got}",
                    expectedSignature, payosSignature);

                await _audit.LogAsync(
                    AuditActions.PaymentWebhookFailed,
                    details: $"Signature mismatch. Path: {Request.Path}",
                    result: AuditResult.Failure,
                    errorMessage: "Invalid signature");

                return Unauthorized(new { error = "Invalid signature" });
            }

            // 2. Parse webhook payload
            PayOsWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<PayOsWebhookPayload>(rawBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse PayOS webhook payload");
                return BadRequest(new { error = "Invalid payload" });
            }

            if (payload?.Data == null)
                return BadRequest(new { error = "Missing data" });

            await _audit.LogAsync(
                AuditActions.PaymentWebhookReceived,
                details: $"PayOS webhook: code={payload.Code}, orderCode={payload.Data.OrderCode}, status={payload.Code}",
                result: AuditResult.Success);

            // 3. Xử lý theo event code
            // PayOS trả "00" là thành công
            if (payload.Code == "00" || payload.Success == true)
            {
                await ProcessPaymentSuccess(payload.Data, rawBody);
            }
            else
            {
                await ProcessPaymentFailed(payload.Data, payload.Code);
            }

            // PayOS yêu cầu trả 200 OK
            return Ok(new { code = "00", desc = "success" });
        }

        private async Task ProcessPaymentSuccess(PayOsWebhookData data, string rawBody)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Tìm payment theo orderCode (idempotency check)
                var payment = await _db.Payments
                    .Include(p => p.Order)
                    .FirstOrDefaultAsync(p => p.TransactionRef == data.PaymentLinkId
                                           || p.TransactionRef == data.OrderCode.ToString());

                if (payment == null)
                {
                    _logger.LogWarning("PayOS webhook: payment not found for orderCode={Code}", data.OrderCode);
                    return;
                }

                // Idempotency: nếu đã PAID rồi thì bỏ qua
                if (payment.Status == PaymentStatuses.Paid)
                {
                    _logger.LogInformation("PayOS webhook: payment already processed. OrderCode={Code}", data.OrderCode);
                    await transaction.RollbackAsync();
                    return;
                }

                // Cập nhật payment
                payment.Status = PaymentStatuses.Paid;
                payment.PaidAt = DateTimeOffset.UtcNow;
                payment.RawResponse = JsonDocument.Parse(rawBody);

                // Cập nhật order
                if (payment.Order != null &&
                    OrderStatuses.CanTransition(payment.Order.Status, OrderStatuses.Paid))
                {
                    payment.Order.PaymentStatus = PaymentStatuses.Paid;
                    payment.Order.Status = OrderStatuses.Paid;
                    payment.Order.ConfirmedAt = DateTimeOffset.UtcNow;
                    payment.Order.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                await _audit.LogAsync(
                    AuditActions.PaymentCompleted,
                    entityType: "Payment",
                    entityId: payment.Id.ToString(),
                    details: $"Payment completed via webhook. OrderCode={data.OrderCode}, Amount={data.Amount}",
                    result: AuditResult.Success);

                _logger.LogInformation("Payment processed successfully. OrderCode={Code}, Amount={Amount}",
                    data.OrderCode, data.Amount);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process payment success webhook");

                await _audit.LogAsync(
                    AuditActions.PaymentWebhookFailed,
                    details: $"Exception processing payment success. OrderCode={data.OrderCode}",
                    result: AuditResult.Failure,
                    errorMessage: ex.Message);
            }
        }

        private async Task ProcessPaymentFailed(PayOsWebhookData data, string? code)
        {
            var payment = await _db.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.TransactionRef == data.PaymentLinkId);

            if (payment == null) return;

            if (payment.Status == PaymentStatuses.Failed || payment.Status == PaymentStatuses.Cancelled)
                return;

            payment.Status = PaymentStatuses.Failed;

            if (payment.Order != null)
            {
                payment.Order.PaymentStatus = PaymentStatuses.Failed;
                payment.Order.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                AuditActions.PaymentWebhookReceived,
                entityType: "Payment",
                entityId: payment.Id.ToString(),
                details: $"Payment failed via webhook. Code={code}, OrderCode={data.OrderCode}",
                result: AuditResult.Failure);
        }

        /// <summary>API endpoint để frontend poll payment status</summary>
        [HttpGet]
        public async Task<IActionResult> Status(Guid orderId)
        {
            var payment = await _db.Payments
                .AsNoTracking()
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payment == null)
                return NotFound(new { ok = false, message = "Payment not found" });

            return Json(new
            {
                ok = true,
                paymentStatus = payment.Status,
                paidAt = payment.PaidAt
            });
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    // PayOS Webhook DTOs
    public class PayOsWebhookPayload
    {
        public string? Code { get; set; }
        public string? Desc { get; set; }
        public bool? Success { get; set; }
        public PayOsWebhookData? Data { get; set; }
        public string? Signature { get; set; }
    }

    public class PayOsWebhookData
    {
        public int OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? AccountNumber { get; set; }
        public string? Reference { get; set; }
        public string? TransactionDateTime { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? Code { get; set; }
        public string? Desc { get; set; }
        public string? CounterAccountBankId { get; set; }
        public string? CounterAccountBankName { get; set; }
        public string? CounterAccountName { get; set; }
        public string? CounterAccountNumber { get; set; }
        public string? VirtualAccountName { get; set; }
        public string? VirtualAccountNumber { get; set; }
        public string? Currency { get; set; }
    }
}
