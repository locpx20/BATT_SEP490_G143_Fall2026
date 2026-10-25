using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("Customer")]
    public class Customer
    {
        [Key]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

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

        [Column("payment_terms")]
        public string? PaymentTerms { get; set; }

        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "Active";

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public User? Creator { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
