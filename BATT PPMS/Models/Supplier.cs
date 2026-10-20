using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("Supplier")]
    public class Supplier
    {
        [Key]
        [Column("supplier_id")]
        public int SupplierId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("supplier_name")]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("contact_person")]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        [Column("phone")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [Column("email")]
        public string? Email { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "Active";

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public User? Creator { get; set; }

        public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();
    }
}
