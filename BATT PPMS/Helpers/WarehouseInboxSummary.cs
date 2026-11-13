using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Helpers;

public sealed record WarehouseInboxSummary(int Imports, int Consumption, int FinishedReceipts = 0, int Plans = 0, int FinishedStock = 0)
{
    public int Total => Imports + Consumption;
    public int FinishedTotal => FinishedReceipts + FinishedStock;

    public static async Task<WarehouseInboxSummary> ReadAsync(ProductionManagementDbContext db, int userId, CancellationToken cancellationToken = default)
    {
        var imports = await db.PurchaseReceipts.CountAsync(r => r.Status == null || r.Status == "Pending"
            || r.Status == "WaitWarehouse" || r.Status == "ImportRejected", cancellationToken);
        var consumption = await db.MaterialUsageReviews.CountAsync(r => r.Status == "Pending", cancellationToken);
        var finishedReceipts = await db.FinishedGoodsReceipts.CountAsync(r => r.Status == "Pending", cancellationToken);
        var plans = await WarehouseNavigationNotices.CountAsync(db, userId, WarehouseNavigationNotice.Plans, cancellationToken);
        var finishedStock = await WarehouseNavigationNotices.CountAsync(db, userId, WarehouseNavigationNotice.FinishedStock, cancellationToken);
        return new(imports, consumption, finishedReceipts, plans, finishedStock);
    }
}

public sealed record WarehouseNavigationModel(string ActivePage, WarehouseInboxSummary Counts);
