using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("QCInspection")]
    public class QCInspection
    {
        [Key]
        [Column("inspection_id")]
        public int InspectionId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("inspection_code")]
        public string InspectionCode { get; set; } = string.Empty;

        [Required]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [Column("request_date")]
        public DateTime RequestDate { get; set; }

        [MaxLength(20)]
        [Column("result")]
        public string? Result { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Required]
        [Column("requested_by")]
        public int RequestedBy { get; set; }

        [Column("inspected_by")]
        public int? InspectedBy { get; set; }

        [Column("inspect_date")]
        public DateTime? InspectDate { get; set; }

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [ForeignKey("RequestedBy")]
        public User? Requester { get; set; }

        [ForeignKey("InspectedBy")]
        public User? Inspector { get; set; }
    }
}
