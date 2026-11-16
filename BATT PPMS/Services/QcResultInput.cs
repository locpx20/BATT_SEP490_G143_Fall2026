namespace BATT_PPMS.Services;

public sealed class QcResultInput
{
    public int RequestId { get; set; }
    public string? PelletColor { get; set; }
    public string? PelletDescription { get; set; }
    public decimal? PelletLengthMm { get; set; }
    public string? Compaction { get; set; }
    public decimal? AshPercent { get; set; }
    public bool? HasImpurities { get; set; }
    public string? Grade { get; set; }
    public string? Result { get; set; }
    public string? Note { get; set; }
}
