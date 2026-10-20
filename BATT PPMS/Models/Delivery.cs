using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("Delivery")]
    public class Delivery
    {
        [Key]
        [Column("delivery_id")]
        public int DeliveryId { get; set; }

        [Required]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("license_plate")]
        public string LicensePlate { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("driver_name")]
        public string DriverName { get; set; } = string.Empty;

        [Required]
        [Column("pickup_date")]
        public DateTime PickupDate { get; set; }

        [MaxLength(50)]
        [Column("delivery_status")]
        public string DeliveryStatus { get; set; } = "Prepared";

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }
    }
}
