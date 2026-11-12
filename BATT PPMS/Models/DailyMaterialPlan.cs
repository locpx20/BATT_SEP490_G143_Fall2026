namespace BATT_PPMS.Models;

public class DailyMaterialPlan
{
    public DateOnly PlanDate { get; set; }
    public decimal ExpectedKg { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User? Planner { get; set; }
}
