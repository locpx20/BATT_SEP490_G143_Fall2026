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
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "receipts";
    [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly PlanDate { get; set; } = DateOnly.FromDateTime(ProductionFlow.Now);
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public PageWindow Pagination { get; set; } = new(0, 1);
    public List<FinishedGoodsReceipt> Receipts { get; set; } = new();
    public List<PersonnelAssignment> Plans { get; set; } = new();
    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }
    public async Task<IActionResult> OnGetAsync() {
        var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Manager, RoleConstants.Owner, RoleConstants.Staff);
        if (check != null) return check;
        if (Tab is not ("plans" or "receipts")) Tab = "receipts";
        if (IsStaff && Tab == "plans") return RedirectToPage("/Personnel/Index");
        var page = PageWindow.ReadRequestedPage(Request.Query, PageNumber);
        if (Tab == "plans") {
            var query = db.PersonnelAssignments.AsNoTracking().Include(x => x.Staff).Include(x => x.AssignedByNavigation).Where(x => x.AssignmentDate == PlanDate);
            Pagination = new(await query.CountAsync(), page);
            Plans = await query.OrderBy(x => x.Shift).ThenBy(x => x.AssignmentId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
        } else {
            var query = db.FinishedGoodsReceipts.AsNoTracking().Include(x => x.Requester).Include(x => x.Receiver)
                .Include(x => x.QualityRequest).ThenInclude(x => x.Assignment)
                .Include(x => x.QualityRequest).ThenInclude(x => x.Report)
                .Include(x => x.QualityRequest).ThenInclude(x => x.Inspection).ThenInclude(x => x.Batch).AsQueryable();
            if (IsStaff) query = query.Where(x => x.RequestedBy == CurrentUserId);
            if (!string.IsNullOrWhiteSpace(StatusFilter)) query = query.Where(x => x.Status == StatusFilter);
            if (!string.IsNullOrWhiteSpace(SearchTerm)) { var term = SearchTerm.Trim(); query = query.Where(x => x.QualityRequest.Inspection.Batch.BagNumber.Contains(term) || (x.Requester != null && x.Requester.FullName.Contains(term))); }
            Pagination = new(await query.CountAsync(), page);
            Receipts = await query.OrderByDescending(x => x.ReceiptId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
        }
        return Page();
    }
    private async Task<IActionResult> Act(Func<Task<int>> action, string success, string tab) {
        if (!ModelState.IsValid) { ErrorMessage = "Dữ liệu nhập không hợp lệ. Vui lòng kiểm tra lại."; return RedirectToPage(new { tab }); }
        try { await action(); SuccessMessage = success; } catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
        return RedirectToPage(new { tab });
    }
    public Task<IActionResult> OnPostRequestReceiptAsync(int requestId, string? note) {
        var check = RequireRole(RoleConstants.Staff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).RequestReceipt(CurrentUserId, requestId, note), "Đã gửi yêu cầu nhập thành phẩm cho Thủ kho.", "receipts");
    }
    public Task<IActionResult> OnPostReceiveAsync(int receiptId, decimal actualQuantity, string? note) {
        var check = RequireRole(RoleConstants.WarehouseStaff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).ReceiveGoods(CurrentUserId, receiptId, actualQuantity, note), "Đã lập phiếu nhập thành phẩm. Kết quả đối chiếu được chuyển theo luồng xác nhận.", "receipts");
    }
    public Task<IActionResult> OnPostRejectAsync(int receiptId, string? reason) {
        var check = RequireRole(RoleConstants.WarehouseStaff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).RejectReceipt(CurrentUserId, receiptId, reason), "Đã trả yêu cầu nhập kho kèm lý do cho Staff.", "receipts");
    }
    public Task<IActionResult> OnPostConfirmAsync(int receiptId) {
        var check = RequireRole(RoleConstants.Staff); if (check != null) return Task.FromResult(check);
        return Act(() => new ProductionFlow(db).ConfirmReceipt(CurrentUserId, receiptId), "Đã xác nhận phiếu nhập kho. Manager có thể xem thông tin nhập kho.", "receipts");
    }
}
