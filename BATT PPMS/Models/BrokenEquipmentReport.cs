using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("BrokenEquipmentReport")]
    public class BrokenEquipmentReport
    {
        [Key]
        [Column("report_id")]
        public int ReportId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("equipment_name")]
        public string EquipmentName { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("reported_by")]
        public int ReportedBy { get; set; }

        [Required]
        [Column("report_date")]
        public DateTime ReportDate { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Reported";

        [MaxLength(20)]
        [Column("severity")]
        public string? Severity { get; set; }

        [ForeignKey("ReportedBy")]
        public User? Reporter { get; set; }

        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
    }
}
