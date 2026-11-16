using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Helpers;

/// <summary>Một lô thành phẩm còn trong kho cùng loại (A/B) theo kết quả QC mới nhất.</summary>
public sealed class GradedLot
{
    public int BatchId { get; set; }
    public string Lot { get; set; } = "";
    public DateOnly ProductionDate { get; set; }
    public decimal StockKg { get; set; }
    public string Grade { get; set; } = "";
}

/// <summary>Lô còn khả dụng để bán (đã trừ phần đã giữ chỗ cho các yêu cầu bán khác).</summary>
public sealed record LotAvailability(int BatchId, string Lot, DateOnly ProductionDate, string Grade, int AvailableBags)
{
    public decimal AvailableKg => AvailableBags * (decimal)FinishedGoodsQuantity.KilogramsPerBag;
}

public sealed record LotAllocation(int BatchId, string Lot, DateOnly ProductionDate, string Grade, int Bags)
{
    public decimal Kg => Bags * (decimal)FinishedGoodsQuantity.KilogramsPerBag;
}

/// <summary>Quy tắc kho thành phẩm cho luồng bán hàng: lấy lô theo FIFO (lô sản xuất cũ nhất trước).</summary>
public static class SalesFlow
{
    public const int KgPerBag = FinishedGoodsQuantity.KilogramsPerBag;
    public static readonly string[] Grades = { "A", "B" };

    /// <summary>Trạng thái yêu cầu bán đang giữ chỗ tồn kho (chưa trừ kho thật).</summary>
    public static readonly string[] ReservingStatuses = { StatusConstants.Pending, StatusConstants.Approved };

    public static bool IsGrade(string? grade) => grade is "A" or "B";

    public static string GradeLabel(string? grade) => grade switch { "A" => "Loại A", "B" => "Loại B", _ => "—" };

    public static string Bags(decimal kg) =>
        (kg / KgPerBag).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Các lô đang "In Stock" đã có kết quả QC đạt và phân loại A/B (cùng cách tính với màn Kho thành phẩm).</summary>
    public static IQueryable<GradedLot> InStockGradedLots(ProductionManagementDbContext db)
    {
        var gradedLots = from request in db.ProductionQualityRequests.AsNoTracking()
                         join result in db.ProductionQcResults on request.RequestId equals result.RequestId
                         where request.Inspection.Status == "Completed" && request.Inspection.Result == "Pass"
                         select new { request.Inspection.BatchId, request.RequestId, result.Grade };
        var latestResults = gradedLots.GroupBy(x => x.BatchId).Select(group => group.Max(x => x.RequestId));
        var latestGrades = gradedLots.Where(x => latestResults.Contains(x.RequestId));
        return from batch in db.FinishedGoodBatches.AsNoTracking()
               where batch.Status == "In Stock" && batch.Quantity > 0
               join grade in latestGrades on batch.BatchId equals grade.BatchId
               select new GradedLot
               {
                   BatchId = batch.BatchId,
                   Lot = batch.BagNumber,
                   ProductionDate = batch.ProductionDate,
                   StockKg = batch.Quantity,
                   Grade = grade.Grade
               };
    }

    /// <summary>
    /// Lô còn bán được của từng loại, sắp xếp FIFO. Số bao khả dụng = tồn kho - phần đã giữ chỗ bởi
    /// yêu cầu bán đang chờ duyệt / đã duyệt (trừ yêu cầu <paramref name="excludeOrderId"/> khi đang sửa).
    /// </summary>
    public static async Task<List<LotAvailability>> AvailableLotsAsync(ProductionManagementDbContext db,
        string? grade = null, int? excludeOrderId = null, CancellationToken ct = default)
    {
        var lotsQuery = InStockGradedLots(db);
        if (grade != null) lotsQuery = lotsQuery.Where(l => l.Grade == grade);
        var lots = await lotsQuery.ToListAsync(ct);
        if (lots.Count == 0) return new();

        var batchIds = lots.Select(l => l.BatchId).ToList();
        var reservedRows = await db.OrderDetails.AsNoTracking()
            .Where(d => batchIds.Contains(d.BatchId) && ReservingStatuses.Contains(d.Order.Status!)
                        && (excludeOrderId == null || d.OrderId != excludeOrderId))
            .Select(d => new { d.BatchId, d.Quantity })
            .ToListAsync(ct);
        var reserved = reservedRows.GroupBy(r => r.BatchId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

        return lots
            .Select(l => new LotAvailability(l.BatchId, l.Lot, l.ProductionDate, l.Grade,
                (int)Math.Floor((l.StockKg - reserved.GetValueOrDefault(l.BatchId)) / KgPerBag)))
            .Where(l => l.AvailableBags > 0)
            .OrderBy(l => l.ProductionDate).ThenBy(l => l.BatchId)
            .ToList();
    }

    /// <summary>FIFO: lấy lô cũ nhất trước cho tới đủ số bao. Trả null nếu không đủ hàng.</summary>
    public static List<LotAllocation>? Allocate(IEnumerable<LotAvailability> lots, int bags)
    {
        var result = new List<LotAllocation>();
        var remaining = bags;
        foreach (var lot in lots.OrderBy(l => l.ProductionDate).ThenBy(l => l.BatchId))
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, lot.AvailableBags);
            if (take <= 0) continue;
            result.Add(new LotAllocation(lot.BatchId, lot.Lot, lot.ProductionDate, lot.Grade, take));
            remaining -= take;
        }
        return remaining > 0 ? null : result;
    }

    /// <summary>Mã chứng từ dạng PREFIX-yyyyMMdd-001 (tăng dần trong ngày).</summary>
    public static string NextCode(IEnumerable<string> existingCodes, string prefix)
    {
        var full = $"{prefix}-{DateTime.Now:yyyyMMdd}-";
        var max = existingCodes
            .Where(c => c.StartsWith(full, StringComparison.Ordinal))
            .Select(c => int.TryParse(c[full.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{full}{max + 1:D3}";
    }

    /// <summary>Gửi thông báo cho mọi tài khoản đang hoạt động của một vai trò (chỉ thêm vào context, chưa SaveChanges).</summary>
    public static async Task NotifyRoleAsync(ProductionManagementDbContext db, string roleName,
        string title, string message, string url, CancellationToken ct = default)
    {
        var userIds = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleName == roleName && (u.Status == null || u.Status == StatusConstants.Active))
            .Select(u => u.UserId)
            .ToListAsync(ct);
        foreach (var id in userIds) Notifier.Add(db, id, title, message, url);
    }

    /// <summary>Báo cho người tạo; nếu tài khoản đã bị xóa thì báo cho mọi Manager.</summary>
    public static async Task NotifyUserOrRoleAsync(ProductionManagementDbContext db, int? userId, string fallbackRole,
        string title, string message, string url, CancellationToken ct = default)
    {
        if (userId.HasValue && await db.Users.AnyAsync(u => u.UserId == userId.Value, ct))
            Notifier.Add(db, userId.Value, title, message, url);
        else
            await NotifyRoleAsync(db, fallbackRole, title, message, url, ct);
    }
}
