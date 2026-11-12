using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Services;

public sealed class WarehouseOverviewData
{
    public decimal RawStockKg { get; set; }
    public decimal FinishedStockKg { get; set; }
    public int FinishedLots { get; set; }
    public PageWindow Pagination { get; set; } = new(0, 1);
    public List<WarehouseMovement> Movements { get; set; } = new();
}

public sealed record WarehouseMovement(int Kind, int Id, DateTime OccurredAt, string Reference,
    string Item, decimal Quantity, string Unit, string Actor)
{
    public bool Incoming => Kind is 1 or 3;
    public string Group => Kind <= 2 ? "Nguyên liệu" : "Thành phẩm";
}

public sealed class WarehouseOverview(ProductionManagementDbContext db)
{
    private sealed class MovementIndex
    {
        public int Kind { get; set; }
        public int Id { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    public async Task<WarehouseOverviewData> ReadAsync(int page, DateOnly? from, DateOnly? to,
        string? direction, string? group, CancellationToken cancellationToken = default)
    {
        var data = new WarehouseOverviewData {
            RawStockKg = await db.RawMaterials.Where(m => m.Unit.Trim().ToLower() == "kg")
                .SumAsync(m => (decimal?)m.CurrentQuantity, cancellationToken) ?? 0,
            FinishedStockKg = await db.FinishedGoodBatches.Where(b => b.Status == "In Stock")
                .SumAsync(b => (decimal?)b.Quantity, cancellationToken) ?? 0,
            FinishedLots = await db.FinishedGoodBatches.CountAsync(b => b.Status == "In Stock" && b.Quantity > 0, cancellationToken)
        };

        var rawImports = db.RawMaterialImports.Where(i => i.Status == "Approved");
        var rawExports = db.RawMaterialExports.AsQueryable();
        var finishedImports = db.FinishedGoodsReceipts.Where(r => r.ReceivedAt != null && r.ActualQuantity > 0
            && (r.Status == "Received" || r.Status == "AwaitingStaffConfirmation"));
        // Delivery has no item allocation of its own. Count each order line once, at its first recorded dispatch.
        var dispatched = db.Deliveries.Where(d => d.DeliveryStatus == "In Transit" || d.DeliveryStatus == "Delivered");
        var firstDispatches = dispatched.Where(d => !dispatched.Any(other => other.OrderId == d.OrderId
            && (other.PickupDate < d.PickupDate || (other.PickupDate == d.PickupDate && other.DeliveryId < d.DeliveryId))));
        var finishedExports = from detail in db.OrderDetails
                              join delivery in firstDispatches on detail.OrderId equals delivery.OrderId
                              select new { Detail = detail, Delivery = delivery };

        var movements = rawImports.Select(i => new MovementIndex { Kind = 1, Id = i.ImportId, OccurredAt = i.ApprovedAt ?? i.ImportDate })
            .Concat(rawExports.Select(e => new MovementIndex { Kind = 2, Id = e.ExportId, OccurredAt = e.ExportDate }))
            .Concat(finishedImports.Select(r => new MovementIndex { Kind = 3, Id = r.ReceiptId, OccurredAt = r.ReceivedAt!.Value }))
            .Concat(finishedExports.Select(e => new MovementIndex { Kind = 4, Id = e.Detail.OrderDetailId, OccurredAt = e.Delivery.PickupDate }));
        if (from.HasValue) {
            var start = from.Value.ToDateTime(TimeOnly.MinValue);
            movements = movements.Where(m => m.OccurredAt >= start);
        }
        if (to.HasValue && to.Value < DateOnly.MaxValue) {
            var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            movements = movements.Where(m => m.OccurredAt < end);
        }
        if (direction == "in") movements = movements.Where(m => m.Kind == 1 || m.Kind == 3);
        if (direction == "out") movements = movements.Where(m => m.Kind == 2 || m.Kind == 4);
        if (group == "raw") movements = movements.Where(m => m.Kind <= 2);
        if (group == "finished") movements = movements.Where(m => m.Kind >= 3);
        data.Pagination = new(await movements.CountAsync(cancellationToken), page);
        var selected = await movements.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Kind).ThenByDescending(m => m.Id)
            .Skip(data.Pagination.Offset).Take(PageWindow.Size).ToListAsync(cancellationToken);
        int[] Ids(int kind) => selected.Where(m => m.Kind == kind).Select(m => m.Id).ToArray();
        var rows = new Dictionary<(int Kind, int Id), WarehouseMovement>();

        var ids = Ids(1);
        if (ids.Length > 0) {
            var items = await rawImports.AsNoTracking().Where(i => ids.Contains(i.ImportId)).Select(i => new {
                i.ImportId, i.ImportCode, At = i.ApprovedAt ?? i.ImportDate, i.Quantity, i.Material.MaterialName, i.Material.Unit,
                Actor = i.ApprovedByNavigation != null ? i.ApprovedByNavigation.FullName : i.ImportedByNavigation != null ? i.ImportedByNavigation.FullName : "Tài khoản đã xóa"
            }).ToListAsync(cancellationToken);
            foreach (var i in items) rows[(1, i.ImportId)] = new(1, i.ImportId, i.At,
                string.IsNullOrWhiteSpace(i.ImportCode) ? $"PNK-{i.ImportId:D4}" : i.ImportCode, i.MaterialName, i.Quantity, i.Unit, i.Actor);
        }
        ids = Ids(2);
        if (ids.Length > 0) {
            var items = await rawExports.AsNoTracking().Where(e => ids.Contains(e.ExportId)).Select(e => new {
                e.ExportId, e.ExportDate, e.Quantity, e.Material.MaterialName, e.Material.Unit,
                ReportId = db.ShiftMaterialUsages.Where(u => u.ExportId == e.ExportId).Select(u => (int?)u.ReportId).FirstOrDefault(),
                Actor = e.ExportedByNavigation != null ? e.ExportedByNavigation.FullName : "Tài khoản đã xóa"
            }).ToListAsync(cancellationToken);
            foreach (var e in items) rows[(2, e.ExportId)] = new(2, e.ExportId, e.ExportDate,
                e.ReportId.HasValue ? $"TH-{e.ReportId:D4}" : $"XNL-{e.ExportId:D4}", e.MaterialName, e.Quantity, e.Unit, e.Actor);
        }
        ids = Ids(3);
        if (ids.Length > 0) {
            var items = await finishedImports.AsNoTracking().Where(r => ids.Contains(r.ReceiptId)).Select(r => new {
                r.ReceiptId, At = r.ReceivedAt!.Value, Quantity = r.ActualQuantity!.Value,
                Lot = r.QualityRequest.Inspection.Batch.BagNumber,
                Actor = r.Receiver != null ? r.Receiver.FullName : "Tài khoản đã xóa"
            }).ToListAsync(cancellationToken);
            foreach (var r in items) rows[(3, r.ReceiptId)] = new(3, r.ReceiptId, r.At, $"NK-{r.ReceiptId:D4}", r.Lot, r.Quantity, "kg", r.Actor);
        }
        ids = Ids(4);
        if (ids.Length > 0) {
            var items = await finishedExports.AsNoTracking().Where(e => ids.Contains(e.Detail.OrderDetailId)).Select(e => new {
                Id = e.Detail.OrderDetailId, e.Detail.Order.OrderCode, Lot = e.Detail.Batch.BagNumber,
                e.Detail.Quantity, At = e.Delivery.PickupDate,
                Actor = e.Delivery.ConfirmedByNavigation != null ? e.Delivery.ConfirmedByNavigation.FullName : "Tài khoản đã xóa"
            }).ToListAsync(cancellationToken);
            foreach (var e in items) rows[(4, e.Id)] = new(4, e.Id, e.At, e.OrderCode, e.Lot, e.Quantity, "kg", e.Actor);
        }
        data.Movements = selected.Where(m => rows.ContainsKey((m.Kind, m.Id))).Select(m => rows[(m.Kind, m.Id)]).ToList();
        return data;
    }
}
