using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("InventoryItem")]
    public class InventoryItem
    {
        [Key]
        [Column("inventory_id")]
        public int InventoryId { get; set; }

        [Required]
        [Column("item_id")]
        public int ItemId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("item_name")]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("unit")]
        public string Unit { get; set; } = string.Empty;

        [Column("quantity")]
        public int Quantity { get; set; } = 0;

        [Column("min_quantity")]
        public int MinQuantity { get; set; } = 0;

        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "Available";

        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; } = new List<PurchaseInvoiceDetail>();
    }
}
