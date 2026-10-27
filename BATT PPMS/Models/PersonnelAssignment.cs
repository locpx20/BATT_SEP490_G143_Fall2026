using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("PersonnelAssignment")]
    public class PersonnelAssignment
    {
        [Key]
        [Column("assignment_id")]
        public int AssignmentId { get; set; }

        [Required]
        [Column("staff_id")]
        public int StaffId { get; set; }

        [Required]
        [Column("assigned_by")]
        public int AssignedBy { get; set; }

        [Required]
        [Column("assignment_date")]
        public DateOnly AssignmentDate { get; set; }

        [MaxLength(50)]
        [Column("shift")]
        public string? Shift { get; set; }

        [Column("task_description")]
        public string? TaskDescription { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Assigned";

        /// <summary>Định mức sản lượng (kg) cho công đoạn này.</summary>
        [Column("target_production")]
        public int? TargetProduction { get; set; }

        [ForeignKey("StaffId")]
        public User? Staff { get; set; }

        [ForeignKey("AssignedBy")]
        public User? Assigner { get; set; }
    }
}
