using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("MaintenanceRequest")]
    public class MaintenanceRequest
    {
        [Key]
        [Column("request_id")]
        public int RequestId { get; set; }

        [Required]
        [Column("broken_report_id")]
        public int BrokenReportId { get; set; }

        [Required]
        [Column("requested_by")]
        public int RequestedBy { get; set; }

        [Column("assigned_to")]
        public int? AssignedTo { get; set; }

        [Required]
        [Column("request_date")]
        public DateTime RequestDate { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Pending";

        [MaxLength(20)]
        [Column("priority")]
        public string? Priority { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("completion_note")]
        public string? CompletionNote { get; set; }

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        [ForeignKey("BrokenReportId")]
        public BrokenEquipmentReport? BrokenEquipmentReport { get; set; }

        [ForeignKey("RequestedBy")]
        public User? Requester { get; set; }

        [ForeignKey("AssignedTo")]
        public User? Assignee { get; set; }

        public ICollection<MaterialPurchaseRequest> MaterialPurchaseRequests { get; set; } = new List<MaterialPurchaseRequest>();
    }
}
