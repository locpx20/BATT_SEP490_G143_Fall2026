using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.QC
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }
        public bool StaffView => IsStaff;
        public bool QcView => IsQCStaff;
        public List<ProductionQualityRequest> Requests { get; set; } = new();
        public List<PersonnelAssignment> Assignments { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public bool Legacy { get; set; }
        [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
        public PageWindow Pagination { get; set; } = new(0, 1);
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public bool CanFinish(ProductionQualityRequest q) => IsQCStaff && !Legacy && q.Inspection.Status == "In Progress" && q.Inspection.InspectedBy == CurrentUserId;

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Staff, RoleConstants.QCStaff, RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;
            if (Legacy) {
                var old = _context.Qcinspections.AsNoTracking().Include(x => x.Batch).Include(x => x.RequestedByNavigation).Include(x => x.InspectedByNavigation)
                    .Where(x => !_context.ProductionQualityRequests.Any(q => q.InspectionId == x.InspectionId));
                if (IsStaff) old = old.Where(x => x.RequestedBy == CurrentUserId);
                if (StatusFilter is "Pending" or "In Progress" or "Completed") old = old.Where(x => x.Status == StatusFilter);
                if (!string.IsNullOrWhiteSpace(SearchTerm)) { var term = SearchTerm.Trim(); old = old.Where(x => x.InspectionCode.Contains(term) || x.Batch.BagNumber.Contains(term)); }
                Pagination = new(await old.CountAsync(), PageWindow.ReadRequestedPage(Request.Query, PageNumber));
                var inspections = await old.OrderByDescending(x => x.InspectionId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
                Requests = inspections.Select(x => new ProductionQualityRequest { RequestId = -x.InspectionId, Inspection = x, CreatedAt = x.RequestDate,
                    Assignment = new PersonnelAssignment { AssignmentDate = x.Batch.ProductionDate, Shift = "Chưa liên kết ca" } }).ToList();
                return Page();
            }

            var query = new ProductionFlow(_context).QualityQuery.AsNoTracking();
            if (IsStaff) query = query.Where(x => x.Inspection.RequestedBy == CurrentUserId);
            if (StatusFilter is "Pending" or "In Progress" or "Completed") query = query.Where(x => x.Inspection.Status == StatusFilter);
            if (!string.IsNullOrWhiteSpace(SearchTerm)) {
                var term = SearchTerm.Trim(); query = query.Where(x => x.Inspection.InspectionCode.Contains(term) || x.Inspection.Batch.BagNumber.Contains(term)
                    || (x.Inspection.RequestedByNavigation != null && x.Inspection.RequestedByNavigation.FullName.Contains(term)));
            }
            Pagination = new(await query.CountAsync(), PageWindow.ReadRequestedPage(Request.Query, PageNumber));
            Requests = await query.OrderByDescending(x => x.RequestId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
            if (IsStaff) Assignments = await _context.PersonnelAssignments.AsNoTracking().Where(x => x.StaffId == CurrentUserId && x.Status != "Cancelled"
                && x.AssignmentDate <= DateOnly.FromDateTime(ProductionFlow.Now)).OrderByDescending(x => x.AssignmentDate).ThenBy(x => x.Shift).ToListAsync();

            return Page();
        }
        public async Task<IActionResult> OnPostRequestAsync(int assignmentId, int quantity, string? note) {
            var check = RequireRole(RoleConstants.Staff); if (check != null) return check;
            if (!ModelState.IsValid) { ErrorMessage = "Kiểm tra lại phân công và sản lượng cần kiểm tra."; return RedirectToPage(); }
            try { await new ProductionFlow(_context).RequestQc(CurrentUserId, assignmentId, quantity, note); SuccessMessage = "Đã gửi yêu cầu kiểm tra. QC sẽ trả kết quả trước khi bạn lập báo cáo ca."; }
            catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostStartAsync(int requestId) {
            var check = RequireRole(RoleConstants.QCStaff); if (check != null) return check;
            try { await new ProductionFlow(_context).StartQc(CurrentUserId, requestId); SuccessMessage = "Đã tiếp nhận yêu cầu kiểm tra."; }
            catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostFinishAsync(int requestId, string? result, string? note) {
            var check = RequireRole(RoleConstants.QCStaff); if (check != null) return check;
            if (!ModelState.IsValid) { ErrorMessage = "Dữ liệu kết quả kiểm tra không hợp lệ."; return RedirectToPage(); }
            try { await new ProductionFlow(_context).FinishQc(CurrentUserId, requestId, result, note); SuccessMessage = "Đã trả kết quả cho Staff. Manager và Owner có thể xem cùng kết quả này."; }
            catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }
    }
}
