using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BATT_PPMS.Pages.Warehouse;

public class ConsumptionModel(ProductionManagementDbContext db) : BasePageModel
{
    public bool CanReview => IsWarehouseStaff;
    public bool StaffView => IsStaff;
    public bool CanCreateConsumption => StaffView && EditId == 0 && Assignments.Count > 0;
    private HashSet<(DateOnly Date, string? Shift)> _chippingShifts = [];
    public string PaginationTarget => "/Warehouse/Consumption";
    public string FilterParameter => "StatusFilter";
    public string PageParameter => "pageNumber";
    public Dictionary<string, string?> PaginationFilters => new()
    {
        ["StatusFilter"] = StatusFilter,
        ["ShiftFilter"] = ShiftFilter,
        ["DateFilter"] = NavigationDateInput
    };
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int EditId { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? ShiftFilter { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? DateFilter { get; set; }
    public string? DateFilterInput => DateFilter?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    public List<string?> ConsumptionShifts { get; set; } = new();
    public DateOnly SelectedDate { get; private set; }
    public string NavigationDateInput => SelectedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    [BindProperty] public int AssignmentId { get; set; }
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public decimal QuantityKg { get; set; }
    [BindProperty] public List<MaterialUsageInput> MaterialsUsed { get; set; } = new();
    public PageWindow Pagination { get; set; } = new(0, 1);
    public List<MaterialUsageReview> Reviews { get; set; } = new();
    public List<ShiftMaterialUsage> Usage { get; set; } = new();
    public List<RawMaterial> Materials { get; set; } = new();
    public List<PersonnelAssignment> Assignments { get; set; } = new();
    public List<AppNotification> Notices { get; set; } = new();
    public DateOnly Today => DateOnly.FromDateTime(ProductionFlow.Now);
    public Dictionary<DateOnly,DailyMaterialPlan> DailyPlans { get; set; } = new();
    public Dictionary<(DateOnly Date, string Shift), ShiftProductionPlan> ShiftPlans { get; private set; } = new();
    public decimal AvailableStockKg { get; set; }
    public decimal ApprovedTodayKg { get; set; }
    public int WaitingCount { get; set; }
    [TempData] public string? ErrorMessage { get; set; }
    [TempData] public string? SuccessMessage { get; set; }

    public bool CanEditConsumption(MaterialUsageReview review)
    {
        var assignment = review.Report.Assignment;
        return StaffView && review.Report.StaffId == CurrentUserId
            && review.Status is "Pending" or "Rejected"
            && assignment.StaffId == CurrentUserId && assignment.Status != "Cancelled"
            && _chippingShifts.Contains((assignment.AssignmentDate, assignment.Shift));
    }

    public async Task<IActionResult> OnGetAsync() {
        var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Staff, RoleConstants.Manager, RoleConstants.Owner);
        if(check != null) return check;
        WaitingCount=await db.MaterialUsageReviews.CountAsync(x=>x.Status=="Pending");

        if (IsWarehouseStaff && !Request.Query.ContainsKey(FilterParameter))
            StatusFilter = "Pending";
        else if (StatusFilter is not ("Pending" or "Rejected" or "Approved"))
            StatusFilter = "All";

        var query = db.MaterialUsageReviews.AsNoTracking().Include(x=>x.Report).ThenInclude(x=>x.Staff)
            .Include(x=>x.Report).ThenInclude(x=>x.Assignment).Include(x=>x.Reviewer).AsQueryable();
        if(IsStaff) query=query.Where(x=>x.Report.StaffId==CurrentUserId);
        var statusQuery = StatusFilter == "All" ? query : query.Where(x => x.Status == StatusFilter);
        SelectedDate = DateFilter ?? await statusQuery.OrderByDescending(x => x.ReportId)
            .Select(x => (DateOnly?)x.Report.ShiftDate).FirstOrDefaultAsync() ?? Today;

        ConsumptionShifts = await query.Where(x => x.Report.ShiftDate == SelectedDate)
            .Select(x => x.Report.Assignment.Shift).Where(x => x != null && x.Trim() != "")
            .Distinct().OrderBy(x => x).ToListAsync();
        query = statusQuery.Where(x => x.Report.ShiftDate == SelectedDate);
        if (!string.IsNullOrWhiteSpace(ShiftFilter) && !ConsumptionShifts.Contains(ShiftFilter)) {
            ShiftFilter = null;
        }
        if(!string.IsNullOrWhiteSpace(ShiftFilter)) {
            query = query.Where(x => x.Report.Assignment.Shift == ShiftFilter);
        }
        var usagePage=PageWindow.ReadRequestedPage(Request.Query,PageNumber);
        Pagination=new(await query.CountAsync(), usagePage);
        Reviews=await query.OrderByDescending(x=>x.ReportId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
        var ids=Reviews.Select(x=>x.ReportId).ToList();
        Usage=await db.ShiftMaterialUsages.AsNoTracking().Include(x=>x.Material).Where(x=>ids.Contains(x.ReportId)).ToListAsync();
        var dates=Reviews.Select(x=>x.Report.ShiftDate).Append(Today).Distinct().ToList();
        DailyPlans=await db.DailyMaterialPlans.AsNoTracking().Where(x=>dates.Contains(x.PlanDate)).ToDictionaryAsync(x=>x.PlanDate);
        ShiftPlans = await db.ShiftProductionPlans.AsNoTracking().Where(plan => dates.Contains(plan.PlanDate))
            .ToDictionaryAsync(plan => (plan.PlanDate, plan.Shift));
        AvailableStockKg=await db.RawMaterials.Where(x=>x.Unit.Trim().ToLower()=="kg").SumAsync(x=>(decimal?)x.CurrentQuantity)??0;
        ApprovedTodayKg=await db.ShiftMaterialUsages.Where(x=>x.Report.ShiftDate==Today&&x.ExportId!=null).SumAsync(x=>(decimal?)x.Quantity)??0;
        if (IsStaff) {
            var flow = new ProductionFlow(db);
            var edit = EditId > 0
                ? await db.MaterialUsageReviews.AsNoTracking().Include(x => x.Report).ThenInclude(x => x.Assignment)
                    .SingleOrDefaultAsync(x => x.ReportId == EditId && x.Report.StaffId == CurrentUserId)
                : null;
            var editDates = Reviews.Where(x => x.Status is "Pending" or "Rejected")
                .Select(x => x.Report.Assignment.AssignmentDate);
            if (edit != null) editDates = editDates.Append(edit.Report.Assignment.AssignmentDate);
            _chippingShifts = await flow.ChippingShiftsAsync(CurrentUserId, editDates.Distinct().ToArray());

            if (EditId > 0) {
                if (edit != null && CanEditConsumption(edit)) {
                    AssignmentId=edit.Report.AssignmentId; Note=edit.Report.Note;
                    Assignments = [edit.Report.Assignment];
                    MaterialsUsed=await db.ShiftMaterialUsages.AsNoTracking().Where(x=>x.ReportId==EditId).Select(x=>new MaterialUsageInput{MaterialId=x.MaterialId,Quantity=x.Quantity}).ToListAsync();
                    QuantityKg=MaterialsUsed.Sum(x=>x.Quantity??0);
                } else {
                    EditId = 0;
                    ErrorMessage = edit?.Status is "Pending" or "Rejected"
                        ? "Bạn cần được phân công Máy băm trong ca này để sửa phiếu tiêu hao."
                        : "Phiếu đã duyệt hoặc không thuộc tài khoản của bạn.";
                }
            }
            if (EditId == 0) Assignments = await flow.ConsumptionShiftOptions(CurrentUserId);
        }
        Notices=await Notifier.TakeUnreadAsync(db,CurrentUserId,preserveWarehouseNavigationNotices:IsWarehouseStaff);
        return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        var check = RequireRole(RoleConstants.Staff);
        if (check != null) return check;
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Vui lòng kiểm tra ca và số lượng nguyên liệu.";
            return RedirectToPage(new { EditId, DateFilter = DateFilterInput });
        }

        int reportId;
        try
        {
            reportId = await new ProductionFlow(db).SaveConsumptionKg(CurrentUserId, AssignmentId, QuantityKg, Note, EditId);
        }
        catch (ProductionFlowException ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage(new { EditId, DateFilter = DateFilterInput });
        }

        var shiftDate = await db.EndOfShiftReports.Where(report => report.ReportId == reportId)
            .Select(report => report.ShiftDate).SingleAsync();
        SuccessMessage = "Đã gửi khối lượng nguyên liệu đã sử dụng cho Thủ kho.";
        return RedirectToPage(new { DateFilter = shiftDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });
    }
    public async Task<IActionResult> OnPostReviewAsync(int reportId,bool approve,string? reason) {
        var check=RequireRole(RoleConstants.WarehouseStaff);if(check!=null)return check;
        if(!ModelState.IsValid){ErrorMessage="Dữ liệu không hợp lệ.";return RedirectToPage(new{StatusFilter="Pending"});}
        try{await new ProductionFlow(db).ReviewMaterialUsage(CurrentUserId,reportId,approve,reason);SuccessMessage=approve?"Đã duyệt tiêu hao và trừ tồn nguyên liệu.":"Đã gửi lý do từ chối cho Staff.";}
        catch(ProductionFlowException ex){ErrorMessage=ex.Message;}
        return RedirectToPage(new{StatusFilter="Pending"});
    }
}
