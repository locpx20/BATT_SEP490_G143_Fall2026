using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BATT_PPMS.Pages.ShiftReports;

public class IndexModel(ProductionManagementDbContext db) : BasePageModel
{
    private static readonly IReadOnlyDictionary<string, string> MaterialStatuses = new Dictionary<string, string>
    {
        ["Pending"] = "Chờ Thủ kho xử lý",
        ["Approved"] = "Đã duyệt và trừ tồn",
        ["Rejected"] = "Từ chối"
    };

    private static readonly IReadOnlyDictionary<string, string> ReceiptStatuses = new Dictionary<string, string>
    {
        ["Pending"] = "Chờ Thủ kho xử lý",
        ["Rejected"] = "Từ chối",
        ["Received"] = "Đồng ý, đã nhập kho"
    };

    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? ReportDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? ShiftFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    public bool ShowReceipts => Tab == "receipts";
    public string? DateInput => ReportDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public IReadOnlyDictionary<string, string> Statuses => ShowReceipts ? ReceiptStatuses : MaterialStatuses;
    public List<string?> Shifts { get; private set; } = [];
    public List<ReportRow> Reports { get; private set; } = [];
    public List<AppNotification> Notices { get; private set; } = [];
    public PageWindow Pagination { get; private set; } = new(0, 1);
    public Dictionary<string, string?> PaginationFilters => new()
    {
        [nameof(Tab)] = Tab,
        [nameof(ReportDate)] = DateInput,
        [nameof(ShiftFilter)] = ShiftFilter,
        [nameof(StatusFilter)] = StatusFilter
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var check = RequireRole(RoleConstants.Staff, RoleConstants.Manager, RoleConstants.Owner);
        if (check != null) return check;
        if (!IsOwnerOrManager) return RedirectToPage("/Warehouse/Consumption");

        Tab = ShowReceipts ? "receipts" : "materials";
        if (ShowReceipts && StatusFilter == "AwaitingStaffConfirmation")
            StatusFilter = "Received";
        var query = ShowReceipts ? ReadReceipts() : ReadMaterials();
        if (ReportDate is { } date)
            query = query.Where(row => row.Date == date);

        Shifts = await query.Select(row => row.Shift).Where(shift => shift != null && shift != "")
            .Distinct().OrderBy(shift => shift).ToListAsync(cancellationToken);
        if (!string.IsNullOrEmpty(ShiftFilter) && Shifts.Contains(ShiftFilter))
            query = query.Where(row => row.Shift == ShiftFilter);
        else
            ShiftFilter = null;

        if (StatusFilter != null && Statuses.ContainsKey(StatusFilter))
            query = query.Where(row => row.Status == StatusFilter);
        else
            StatusFilter = null;

        Pagination = new PageWindow(await query.CountAsync(cancellationToken), PageWindow.ReadRequestedPage(Request.Query));
        Reports = await query.OrderByDescending(row => row.Date).ThenByDescending(row => row.Id)
            .Skip(Pagination.Offset).Take(Pagination.PageSize).ToListAsync(cancellationToken);
        await LoadShiftComparisonsAsync(cancellationToken);
        if (ShowReceipts)
            Notices = await Notifier.TakeUnreadAsync(db, CurrentUserId, urlPrefix: "/ShiftReports");
        return Page();
    }

    private async Task LoadShiftComparisonsAsync(CancellationToken cancellationToken)
    {
        if (Reports.Count == 0) return;
        var dates = Reports.Select(r => r.Date).Distinct().ToArray();
        var plans = await db.ShiftProductionPlans.AsNoTracking()
            .Where(p => dates.Contains(p.PlanDate) && !p.IsDayOff)
            .ToDictionaryAsync(p => (p.PlanDate, p.Shift), cancellationToken);
        var reviews = await db.MaterialUsageReviews.AsNoTracking()
            .Where(r => r.Status == "Approved" && dates.Contains(r.Report.ShiftDate))
            .Select(r => new {
                Date = r.Report.ShiftDate, r.Report.Assignment.Shift,
                HasUsage = db.ShiftMaterialUsages.Any(u => u.ReportId == r.ReportId),
                Invalid = db.ShiftMaterialUsages.Any(u => u.ReportId == r.ReportId && (u.ExportId == null || u.Material.Unit.Trim().ToLower() != "kg")),
                Kg = db.ShiftMaterialUsages.Where(u => u.ReportId == r.ReportId).Sum(u => (decimal?)u.Quantity) ?? 0
            }).ToListAsync(cancellationToken);
        var usage = reviews.GroupBy(r => (r.Date, r.Shift)).ToDictionary(g => g.Key,
            g => g.All(r => r.HasUsage && !r.Invalid) ? (decimal?)g.Sum(r => r.Kg) : null);
        var receipts = await db.FinishedGoodsReceipts.AsNoTracking()
            .Where(r => dates.Contains(r.QualityRequest.Assignment.AssignmentDate) && r.ReceivedAt != null
                && r.ActualQuantity.HasValue && (r.Status == "Received" || r.Status == "AwaitingStaffConfirmation"))
            .Select(r => new { Date = r.QualityRequest.Assignment.AssignmentDate, r.QualityRequest.Assignment.Shift, Kg = r.ActualQuantity!.Value })
            .ToListAsync(cancellationToken);
        var received = receipts.GroupBy(r => (r.Date, r.Shift)).ToDictionary(g => g.Key, g => g.Sum(r => r.Kg));
        foreach (var report in Reports)
        {
            if (string.IsNullOrWhiteSpace(report.Shift)) continue;
            var key = (report.Date, report.Shift);
            plans.TryGetValue(key, out var plan);
            report.Comparison = new ShiftComparison(plan?.ExpectedMaterialKg, usage.GetValueOrDefault(key),
                plan?.ExpectedFinishedKg, received.TryGetValue(key, out var kg) ? kg : null);
        }
    }

    public sealed record ShiftComparison(decimal? ExpectedMaterialKg, decimal? ActualMaterialKg,
        decimal? ExpectedFinishedKg, decimal? ActualFinishedKg);

    private IQueryable<ReportRow> ReadMaterials() => db.MaterialUsageReviews.AsNoTracking().Select(review => new ReportRow
    {
        Id = review.ReportId,
        Date = review.Report.ShiftDate,
        Shift = review.Report.Assignment.Shift,
        Sender = review.Report.Staff == null ? null : review.Report.Staff.FullName,
        Quantity = db.ShiftMaterialUsages.Where(usage => usage.ReportId == review.ReportId).Sum(usage => (decimal?)usage.Quantity),
        Status = review.Status,
        Note = review.Report.Note,
        RejectionReason = review.Status == "Rejected" ? review.RejectionReason : null
    });

    private IQueryable<ReportRow> ReadReceipts() => db.FinishedGoodsReceipts.AsNoTracking().Select(receipt => new ReportRow
    {
        Id = receipt.ReceiptId,
        Date = receipt.QualityRequest.Assignment.AssignmentDate,
        Shift = receipt.QualityRequest.Assignment.Shift,
        Sender = receipt.Requester == null ? null : receipt.Requester.FullName,
        Lot = receipt.QualityRequest.Inspection.Batch.BagNumber,
        Quantity = receipt.ExpectedQuantity,
        ReceivedQuantity = receipt.ActualQuantity,
        ReceivedAt = receipt.ReceivedAt,
        Status = receipt.Status == "AwaitingStaffConfirmation" ? "Received" : receipt.Status,
        Note = receipt.RequestNote,
        RejectionReason = receipt.Status == "Rejected" ? receipt.RejectionReason : null
    });

    public sealed class ReportRow
    {
        public int Id { get; init; }
        public DateOnly Date { get; init; }
        public string? Shift { get; init; }
        public string? Sender { get; init; }
        public string? Lot { get; init; }
        public decimal? Quantity { get; init; }
        public decimal? ReceivedQuantity { get; init; }
        public DateTime? ReceivedAt { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Note { get; init; }
        public string? RejectionReason { get; init; }
        public ShiftComparison? Comparison { get; set; }
    }
}
