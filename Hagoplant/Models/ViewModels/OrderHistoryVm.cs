using System;
using System.Collections.Generic;
using System.Linq;
using Hagoplant.Models.ViewModels; // để dùng ShippingAddressVm

namespace Hagoplant.Models.ViewModels
{
    public class OrderHistoryVm
    {
        public List<OrderCardVm> Orders { get; set; } = new();
        public bool IsEmpty => Orders == null || Orders.Count == 0;
    }

    public class OrderCardVm
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }

        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentStatus { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string? VoucherCodeSnapshot { get; set; }

        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public ShippingAddressVm? Shipping { get; set; }

        public List<OrderItemVm> Items { get; set; } = new();
        public List<PaymentVm> Payments { get; set; } = new();

        public int TotalQty => Items?.Sum(x => x.Quantity) ?? 0;
    }

    public class OrderItemVm
    {
        public Guid? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? ImageUrl { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class PaymentVm
    {
        public string? Provider { get; set; }
        public string? Method { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? TransactionRef { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
