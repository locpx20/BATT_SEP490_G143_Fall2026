namespace BATT_PPMS.Models;

public class ShiftMaterialUsage
{
    public int UsageId { get; set; }
    public int ReportId { get; set; }
    public int MaterialId { get; set; }
    public decimal Quantity { get; set; }
    public int? ExportId { get; set; }
    public EndOfShiftReport Report { get; set; } = null!;
    public RawMaterial Material { get; set; } = null!;
    public RawMaterialExport? Export { get; set; }
}

public class MaterialUsageInput
{
    public int MaterialId { get; set; }
    public decimal? Quantity { get; set; }
}
