using Hagoplant.DBcontext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hagoplant.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly HagoDbContext _db;

        public PaymentsController(HagoDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> PayReturn(Guid orderId)
        {
            var payment = await _db.Payments
     .Include(p => p.Order)
     .Where(p => p.OrderId == orderId)
     .OrderByDescending(p => p.CreatedAt)
     .FirstOrDefaultAsync();


            if (payment == null)
                return NotFound();

            // ⚠ DEMO: coi như payOS đã thanh toán thành công
            payment.Status = "PAID";
            payment.PaidAt = DateTimeOffset.UtcNow;

            if (payment.Order != null)
            {
                payment.Order.PaymentStatus = "PAID";
                payment.Order.Status = "CONFIRMED";
            }

            await _db.SaveChangesAsync();

            // redirect lại trang QR (JS polling sẽ thấy PAID)
            return RedirectToAction("Checkout", "Home");
        }
    }
}
