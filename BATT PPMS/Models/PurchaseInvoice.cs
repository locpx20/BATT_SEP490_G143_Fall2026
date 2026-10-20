using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("PurchaseInvoice")]
    public class PurchaseInvoice
    {
        [Key]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("invoice_code")]
        public string InvoiceCode { get; set; } = string.Empty;

        [Required]
        [Column("supplier_id")]
        public int SupplierId { get; set; }

        [Required]
        [Column("purchase_date")]
        public DateTime PurchaseDate { get; set; }

        [Required]
        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [ForeignKey("CreatedBy")]
        public User? Creator { get; set; }

        [ForeignKey("ApprovedBy")]
        public User? Approver { get; set; }

        public ICollection<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; } = new List<PurchaseInvoiceDetail>();
    }
}
