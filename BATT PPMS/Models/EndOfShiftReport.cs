using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("EndOfShiftReport")]
    public class EndOfShiftReport
    {
        [Key]
        [Column("report_id")]
        public int ReportId { get; set; }

        [Required]
        [Column("staff_id")]
        public int StaffId { get; set; }

        [Required]
        [Column("shift_date")]
        public DateOnly ShiftDate { get; set; }

        [Required]
        [Column("production_quantity")]
        public int ProductionQuantity { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Submitted";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(50)]
        [Column("shift")]
        public string? Shift { get; set; }

        [Column("raw_material_used")]
        public int? RawMaterialUsed { get; set; }

        [Column("has_incident")]
        public bool? HasIncident { get; set; }

        [Column("incident_description")]
        public string? IncidentDescription { get; set; }

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        [ForeignKey("StaffId")]
        public User? Staff { get; set; }
    }
}
