namespace BATT_PPMS.Models;

public sealed class ProductionQcResult
{
    public int RequestId { get; set; }
    public string PelletColor { get; set; } = null!;
    public string PelletDescription { get; set; } = null!;
    public decimal PelletLengthMm { get; set; }
    public string Compaction { get; set; } = null!;
    public decimal AshPercent { get; set; }
    public bool HasImpurities { get; set; }
    public string Grade { get; set; } = null!;
}
