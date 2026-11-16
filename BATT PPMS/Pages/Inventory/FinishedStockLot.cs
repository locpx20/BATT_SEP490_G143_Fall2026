using BATT_PPMS.Helpers;

namespace BATT_PPMS.Pages.Inventory;

public sealed class FinishedStockLot
{
    public int BatchId { get; init; }
    public string Lot { get; init; } = "";
    public DateOnly ProductionDate { get; init; }
    public decimal StockKg { get; init; }
    public string? Grade { get; init; }
    public FinishedStockReceipt? Receipt { get; set; }

    public string Location => Grade is "A" or "B" ? $"Kho {Grade}" : "—";
    public string Quality => Grade switch { "A" => "Loại A", "B" => "Loại B", _ => "Chưa phân loại" };
    public string Receiver => Receipt == null ? "—" : Receipt.Receiver ?? "Tài khoản đã xóa";
    public int? TotalBags => LotCode.BagCount(Lot);
    public decimal? RemainingBags => TotalBags.HasValue ? StockKg / FinishedGoodsQuantity.KilogramsPerBag : null;
}

public sealed record FinishedStockReceipt(int ReceiptId, int QualityRequestId, DateTime? ReceivedAt,
    decimal? InitialKg, string? Receiver, string? Note, bool HasQcImage);

public sealed record FinishedStockTotal(decimal StockKg, int Lots);
