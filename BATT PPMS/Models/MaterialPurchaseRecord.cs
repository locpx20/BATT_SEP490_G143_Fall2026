using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("MaterialPurchaseRecord")]
    public class MaterialPurchaseRecord
    {
        [Key]
        [Column("record_id")]
        public int RecordId { get; set; }

        [Required]
        [Column("request_id")]
        public int RequestId { get; set; }

        [Required]
        [Column("purchased_by")]
        public int PurchasedBy { get; set; }

        [Required]
        [Column("purchase_date")]
        public DateTime PurchaseDate { get; set; }

        [Required]
        [Column("actual_cost", TypeName = "decimal(12,2)")]
        public decimal ActualCost { get; set; }

        [MaxLength(255)]
        [Column("invoice_image")]
        public string? InvoiceImage { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [ForeignKey("RequestId")]
        public MaterialPurchaseRequest? MaterialPurchaseRequest { get; set; }

        [ForeignKey("PurchasedBy")]
        public User? Purchaser { get; set; }
    }
}
