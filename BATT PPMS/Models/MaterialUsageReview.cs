namespace BATT_PPMS.Models;

public class MaterialUsageReview
{
    public int ReportId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RejectionReason { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public EndOfShiftReport Report { get; set; } = null!;
    public User? Reviewer { get; set; }
}
