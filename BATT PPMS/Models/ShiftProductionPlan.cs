namespace BATT_PPMS.Models;

public sealed class ShiftProductionPlan
{
    public DateOnly PlanDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public decimal ExpectedMaterialKg { get; set; }
    public decimal ExpectedFinishedKg { get; set; }
    public string? Note { get; set; }
}
