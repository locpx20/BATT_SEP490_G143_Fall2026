using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Warehouse;

public class IndexModel(ProductionManagementDbContext db) : BasePageModel
{
    public bool CanReceive => IsWarehouseStaff;
    public bool StaffView => IsStaff;
    // Navigation is optional on POST: receipt forms submit their own handler fields.
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; } = "receipts";
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? ShiftFilter { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? ReceiptDate { get; set; }
    public string? ReceiptDateInput => ReceiptDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    public DateOnly SelectedReceiptDate { get; private set; } = DateOnly.FromDateTime(ProductionFlow.Now);
    public string? ReceiptNavigationDate => Tab == "receipts" && !StaffView
        ? SelectedReceiptDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        : ReceiptDateInput;
    public List<string?> ReceiptShifts { get; set; } = new();
    [BindProperty(SupportsGet = true)] public DateOnly PlanDate { get; set; } = DateOnly.FromDateTime(ProductionFlow.Now);
    [BindProperty(SupportsGet = true)] public bool AllPlanShifts { get; set; }
    public bool PlanDateInvalid { get; private set; }
    public string? PlanDateInput => (IsWarehouseStaff && !Request.Query.ContainsKey(nameof(PlanDate))) || PlanDateInvalid
        ? null : PlanDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public PageWindow Pagination { get; set; } = new(0, 1);
    public List<FinishedGoodsReceipt> Receipts { get; set; } = new();
    public List<PersonnelAssignment> Plans { get; set; } = new();
    public List<string?> PlanShifts { get; set; } = new();
    public List<ProductionQualityRequest> ReadyQc { get; set; } = new();
    public List<AppNotification> Notices { get; set; } = new();
    public DailyMaterialPlan? DailyPlan { get; set; }
    public IReadOnlyDictionary<string, ShiftProductionPlan> ShiftPlans { get; private set; }
        = new Dictionary<string, ShiftProductionPlan>();
    public decimal AvailableStockKg { get; set; }
    [BindProperty(SupportsGet = true)] public int QualityRequestId { get; set; }
    [BindProperty(SupportsGet = true)] public int? Step { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? QcDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? QcShift { get; set; }
    public DateOnly SelectedQcDate { get; set; }
    public List<string> QcShiftsForDate { get; set; } = new();
    public List<ProductionQualityRequest> FilteredQcRequests { get; set; } = new();
    public int EffectiveStep { get; private set; } = 4;
    public bool StaffReceivedView => StaffView && EffectiveStep == 5;
    public int? StaffReceiptStep => StaffView ? (StaffReceivedView ? 5 : 4) : null;
    public static IReadOnlyList<string> OpenReceiptStatuses { get; } = ["Pending", "Rejected"];
    public DateOnly Today => DateOnly.FromDateTime(ProductionFlow.Now);
    public List<PersonnelAssignment> StaffAssignments { get; set; } = new();

    public static string FormatShift(string? shift) {
        if (string.IsNullOrWhiteSpace(shift)) return "Chưa ghi nhận ca";
        var trimmed = shift.Trim();
        return trimmed.StartsWith("Ca", StringComparison.OrdinalIgnoreCase) ? trimmed : $"Ca {trimmed}";
    }
    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }
    public async Task<IActionResult> OnGetAsync() {
        var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Manager, RoleConstants.Owner, RoleConstants.Staff);
        if (check != null) return check;
        if (Tab is not ("plans" or "receipts")) Tab = "receipts";
        if (IsStaff && Tab == "plans") return RedirectToPage("/Personnel/Index");
        var page = PageWindow.ReadRequestedPage(Request.Query, PageNumber);
        if (Tab == "plans") {
            PlanDateInvalid = Request.Query.ContainsKey(nameof(PlanDate))
                && (string.IsNullOrWhiteSpace(Request.Query[nameof(PlanDate)])
                    || (ModelState.TryGetValue(nameof(PlanDate), out var dateState) && dateState.Errors.Count > 0));
            if (PlanDateInvalid && !IsWarehouseStaff) ErrorMessage = "Vui lòng chọn ngày sản xuất hợp lệ.";
            if (!PlanDateInvalid) {
                ShiftPlans = await db.ShiftProductionPlans.AsNoTracking().Where(plan => plan.PlanDate == PlanDate)
                    .ToDictionaryAsync(plan => plan.Shift);
                if (!IsWarehouseStaff) {
                    DailyPlan=await db.DailyMaterialPlans.AsNoTracking().SingleOrDefaultAsync(x=>x.PlanDate==PlanDate);
                    AvailableStockKg=await db.RawMaterials.Where(x=>x.Unit.Trim().ToLower()=="kg").SumAsync(x=>(decimal?)x.CurrentQuantity)??0;
                }
                var query = db.PersonnelAssignments.AsNoTracking().Include(x => x.Staff).Include(x => x.AssignedByNavigation).Where(x => x.AssignmentDate == PlanDate);
                if (IsWarehouseStaff) {
                    PlanShifts = (await query.Select(x => x.Shift).Distinct().ToListAsync())
                        .Union(ShiftPlans.Values.Where(p => p.IsDayOff).Select(p => (string?)p.Shift))
                        .OrderBy(shift => shift).ToList();
                    if (AllPlanShifts) {
                        Plans = await query.OrderBy(x => x.Shift).ThenBy(x => x.AssignmentId).ToListAsync();
                    } else if (PlanShifts.Count > 0) {
                        Pagination = new(PlanShifts.Count, page, pageSize: 1);
                        var shift = PlanShifts[Pagination.Offset];
                        Plans = await query.Where(x => x.Shift == shift).OrderBy(x => x.AssignmentId).ToListAsync();
                    }
                } else {
                    Pagination = new(await query.CountAsync(), page);
                    Plans = await query.OrderBy(x => x.Shift).ThenBy(x => x.AssignmentId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
                }
            }
        } else {
            if (IsStaff)
                EffectiveStep = Step is >= 1 and <= 5 ? Step.Value : QualityRequestId > 0 ? 3 : 4;

            if (IsStaff && EffectiveStep == 1) {
                StaffAssignments = await new ProductionFlow(db).AssignedShiftOptions(CurrentUserId);
                var existingQc = await db.ProductionQualityRequests.AsNoTracking()
                    .Where(x => x.Inspection.RequestedBy == CurrentUserId && (x.Inspection.Status != "Completed" || x.Inspection.Result == "Pass"))
                    .Select(x => new { x.Assignment.AssignmentDate, x.Assignment.Shift })
                    .ToListAsync();
                StaffAssignments = StaffAssignments
                    .Where(a => !existingQc.Any(q => q.AssignmentDate == a.AssignmentDate && q.Shift == a.Shift))
                    .ToList();
            } else if (IsStaff && EffectiveStep is 2 or 3) {
                var staffQcRequests = await new ProductionFlow(db).QualityQuery.AsNoTracking()
                    .Where(x => x.Inspection.RequestedBy == CurrentUserId && x.Assignment.StaffId == CurrentUserId && x.Assignment.Status != "Cancelled")
                    .OrderByDescending(x => x.RequestId)
                    .ToListAsync();
                ReadyQc = staffQcRequests.Where(x => x.Inspection.Status == "Completed" && x.Inspection.Result == "Pass" && x.Receipt == null).ToList();
                SelectedQcDate = QcDate ?? Today;
                QcShiftsForDate = staffQcRequests
                    .Where(x => x.Assignment.AssignmentDate == SelectedQcDate)
                    .Select(x => x.Assignment.Shift!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                var qcFiltered = staffQcRequests.Where(x => x.Assignment.AssignmentDate == SelectedQcDate);
                if (!string.IsNullOrWhiteSpace(QcShift)) {
                    qcFiltered = qcFiltered.Where(x => string.Equals(x.Assignment.Shift, QcShift, StringComparison.OrdinalIgnoreCase));
                }
                FilteredQcRequests = qcFiltered.ToList();
            } else {
                await LoadReceiptsAsync(page);
            }
        }
        Notices=IsWarehouseStaff && Tab=="plans"
            ? await WarehouseNavigationNotices.OpenAsync(db,CurrentUserId,WarehouseNavigationNotice.Plans,HttpContext.RequestAborted)
            : await Notifier.TakeUnreadAsync(db,CurrentUserId,preserveWarehouseNavigationNotices:IsWarehouseStaff);
        return Page();
    }

    private async Task LoadReceiptsAsync(int page)
    {
        var query = db.FinishedGoodsReceipts.AsNoTracking().Include(x => x.Requester).Include(x => x.Receiver)
                .Include(x => x.QualityRequest).ThenInclude(x => x.Assignment)
                .Include(x => x.QualityRequest).ThenInclude(x => x.Report)
                .Include(x => x.QualityRequest).ThenInclude(x => x.Inspection).ThenInclude(x => x.Batch).AsQueryable();
        if (IsStaff) {
            query = query.Where(x => x.RequestedBy == CurrentUserId);
            // The former confirmation status already recorded the stock movement.
            query = StaffReceivedView
                ? query.Where(x => x.Status == "Received" || x.Status == "AwaitingStaffConfirmation")
                : query.Where(x => OpenReceiptStatuses.Contains(x.Status));
            if (StaffReceivedView || !OpenReceiptStatuses.Contains(StatusFilter ?? ""))
                StatusFilter = null;
        }

        if (StatusFilter == "AwaitingStaffConfirmation")
            StatusFilter = "Received";
        if (string.IsNullOrWhiteSpace(StatusFilter))
            StatusFilter = null;
        var statusQuery = StatusFilter switch
        {
            null => query,
            "Received" => query.Where(x => x.Status == "Received" || x.Status == "AwaitingStaffConfirmation"),
            _ => query.Where(x => x.Status == StatusFilter)
        };
        if (!IsStaff) {
            SelectedReceiptDate = ReceiptDate ?? await statusQuery.OrderByDescending(x => x.ReceiptId)
                .Select(x => (DateOnly?)x.QualityRequest.Assignment.AssignmentDate).FirstOrDefaultAsync() ?? Today;
            query = query.Where(x => x.QualityRequest.Assignment.AssignmentDate == SelectedReceiptDate);
            statusQuery = statusQuery.Where(x => x.QualityRequest.Assignment.AssignmentDate == SelectedReceiptDate);
        } else if (ReceiptDate is { } receiptDate) {
            statusQuery = statusQuery.Where(x => x.QualityRequest.Assignment.AssignmentDate == receiptDate);
        }

        var shiftQuery = IsStaff ? statusQuery : query;
        ReceiptShifts = await shiftQuery.Select(x => x.QualityRequest.Assignment.Shift)
            .Where(x => x != null && x.Trim() != "").Distinct().OrderBy(x => x).ToListAsync();
        if (!string.IsNullOrWhiteSpace(ShiftFilter) && !ReceiptShifts.Contains(ShiftFilter))
            ShiftFilter = null;
        if (!string.IsNullOrWhiteSpace(ShiftFilter))
            statusQuery = statusQuery.Where(x => x.QualityRequest.Assignment.Shift == ShiftFilter);
        Pagination = new(await statusQuery.CountAsync(), page);
        var ordered = StaffReceivedView
            ? statusQuery.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.ReceiptId)
            : statusQuery.OrderByDescending(x => x.ReceiptId);
        Receipts = await ordered.Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
    }
    public async Task<IActionResult> OnGetInboxCountsAsync() {
        var check = RequireRole(RoleConstants.WarehouseStaff);
        if (check != null) return check;
        Response.Headers["Cache-Control"] = "no-store";
        return new JsonResult(await WarehouseInboxSummary.ReadAsync(db, CurrentUserId, HttpContext.RequestAborted));
    }
    private async Task<IActionResult> Act(Func<Task<int>> action, string success, string tab, int? step = null, DateOnly? qcDate = null, string? qcShift = null) {
        if (!ModelState.IsValid) {
            ErrorMessage = "Dữ liệu nhập không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToPage(new { tab, step, qcDate, qcShift });
        }
        try { await action(); SuccessMessage = success; }
        catch (ProductionFlowException ex) {
            ErrorMessage = ex.Message;
            return RedirectToPage(new { tab, step, qcDate, qcShift });
        }
        return RedirectToPage(new { tab, step, qcDate, qcShift });
    }
    public async Task<IActionResult> OnPostRequestQcAsync(int assignmentId, int quantity, string? note) {
        var check = RequireRole(RoleConstants.Staff); if (check != null) return check;
        var assignment = await db.PersonnelAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.AssignmentId == assignmentId);
        var targetDate = assignment?.AssignmentDate ?? Today;
        return await Act(() => new ProductionFlow(db).RequestQc(CurrentUserId, assignmentId, quantity, note),
            "Đã gửi yêu cầu kiểm tra chất lượng cho nhân viên QC.", "receipts", 2, targetDate, assignment?.Shift);
    }
    public Task<IActionResult> OnPostRequestReceiptAsync(int requestId, string? note) {
        var check = RequireRole(RoleConstants.Staff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).RequestReceipt(CurrentUserId, requestId, note), "Đã gửi yêu cầu nhập thành phẩm cho Thủ kho.", "receipts", 4);
    }
    public Task<IActionResult> OnPostReceiveAsync(int receiptId, decimal actualQuantity, string? note) {
        var check = RequireRole(RoleConstants.WarehouseStaff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).ReceiveGoods(CurrentUserId, receiptId, actualQuantity, note),
            "Đã nhập kho thành phẩm và gửi thông báo cho Manager.", "receipts");
    }
    public Task<IActionResult> OnPostRejectAsync(int receiptId, string? reason) {
        var check = RequireRole(RoleConstants.WarehouseStaff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).RejectReceipt(CurrentUserId, receiptId, reason), "Đã trả yêu cầu nhập kho kèm lý do cho Staff.", "receipts");
    }
}
