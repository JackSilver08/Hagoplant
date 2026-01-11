using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Hagoplant.Models
{
    [Table("payments", Schema = "hago")]
    public class Payment
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("order_id")]
        public Guid OrderId { get; set; }

        [Column("provider")]
        public string? Provider { get; set; }

        [Column("method")]
        public string? Method { get; set; }

        [Precision(12, 2)]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("transaction_ref")]
        public string? TransactionRef { get; set; }

        [Column("paid_at")]
        public DateTimeOffset? PaidAt { get; set; }

        // jsonb
        [Column("raw_response", TypeName = "jsonb")]
        public JsonDocument? RawResponse { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        // Navigation
        public Order? Order { get; set; }
    }

}
