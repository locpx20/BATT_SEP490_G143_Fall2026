using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("Order")]
    public class Order
    {
        [Key]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("order_code")]
        public string OrderCode { get; set; } = string.Empty;

        [Required]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Required]
        [Column("item_id")]
        public int ItemId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        [Column("Approved_by")]
        public int? ApprovedBy { get; set; }

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Required]
        [Column("order_date")]
        public DateTime OrderDate { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [ForeignKey("ItemId")]
        public InventoryItem? InventoryItem { get; set; }

        [ForeignKey("ApprovedBy")]
        public User? Approver { get; set; }

        [ForeignKey("CreatedBy")]
        public User? Creator { get; set; }

        public Delivery? Delivery { get; set; }
        public QCInspection? QCInspection { get; set; }
    }
}
