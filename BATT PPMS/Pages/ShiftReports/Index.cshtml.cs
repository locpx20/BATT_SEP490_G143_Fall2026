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
        ["AwaitingStaffConfirmation"] = "Đồng ý, chờ xác nhận",
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
        if (!IsManager) return RedirectToPage("/Warehouse/Consumption");

        Tab = ShowReceipts ? "receipts" : "materials";
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
        return Page();
    }

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
        Status = receipt.Status,
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
    }
}
