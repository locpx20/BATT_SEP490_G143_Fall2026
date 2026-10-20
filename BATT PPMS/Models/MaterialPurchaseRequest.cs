using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("MaterialPurchaseRequest")]
    public class MaterialPurchaseRequest
    {
        [Key]
        [Column("request_id")]
        public int RequestId { get; set; }

        [Required]
        [Column("maintenance_request_id")]
        public int MaintenanceRequestId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("item_name")]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("requested_by")]
        public int RequestedBy { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Required]
        [Column("request_date")]
        public DateTime RequestDate { get; set; }

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [ForeignKey("MaintenanceRequestId")]
        public MaintenanceRequest? MaintenanceRequest { get; set; }

        [ForeignKey("RequestedBy")]
        public User? Requester { get; set; }

        [ForeignKey("ApprovedBy")]
        public User? Approver { get; set; }

        public MaterialPurchaseRecord? MaterialPurchaseRecord { get; set; }
    }
}
