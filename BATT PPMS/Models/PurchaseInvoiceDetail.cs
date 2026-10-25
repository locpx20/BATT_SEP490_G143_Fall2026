using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("PurchaseInvoiceDetail")]
    public class PurchaseInvoiceDetail
    {
        [Key]
        [Column("invoice_detail_id")]
        public int InvoiceDetailId { get; set; }

        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Required]
        [Column("inventory_id")]
        public int InventoryId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [ForeignKey("InvoiceId")]
        public PurchaseInvoice? PurchaseInvoice { get; set; }

        [ForeignKey("InventoryId")]
        public InventoryItem? InventoryItem { get; set; }
    }
}
