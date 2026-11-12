using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.QC
{
    [RequestSizeLimit(6 * 1024 * 1024)]
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }
        public bool StaffView => IsStaff;
        public bool QcView => IsQCStaff;
        public List<ProductionQualityRequest> Requests { get; set; } = new();
        public List<PersonnelAssignment> Assignments { get; set; } = new();
        public List<AppNotification> Notices { get; set; } = new();
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
            Notices=await Notifier.TakeUnreadAsync(_context,CurrentUserId);
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
            if (IsStaff) Assignments = await new ProductionFlow(_context).AssignedShiftOptions(CurrentUserId);

            return Page();
        }
        public async Task<IActionResult> OnPostRequestAsync(int assignmentId, int quantity, string? note) {
            var check = RequireRole(RoleConstants.Staff); if (check != null) return check;
            if (!ModelState.IsValid) { ErrorMessage = "Kiểm tra lại ca và sản lượng cần kiểm tra."; return RedirectToPage(); }
            try { await new ProductionFlow(_context).RequestQc(CurrentUserId, assignmentId, quantity, note); SuccessMessage = "Đã gửi yêu cầu tới nhân viên QC để tiếp nhận và kiểm tra chất lượng."; }
            catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostStartAsync(int requestId) {
            var check = RequireRole(RoleConstants.QCStaff); if (check != null) return check;
            try { await new ProductionFlow(_context).StartQc(CurrentUserId, requestId); SuccessMessage = "Đã tiếp nhận yêu cầu kiểm tra."; }
            catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostFinishAsync(int requestId, string? result, string? note, IFormFile? image) {
            var check = RequireRole(RoleConstants.QCStaff); if (check != null) return check;
            if (!ModelState.IsValid) { ErrorMessage = "Dữ liệu kết quả kiểm tra không hợp lệ."; return RedirectToPage(); }
            try {
                if(image == null || image.Length == 0 || image.Length > QcEvidence.MaxBytes) throw new ProductionFlowException("Vui lòng đính kèm ảnh kết quả, tối đa 5 MB.");
                using var buffer = new MemoryStream(); await image.CopyToAsync(buffer);
                await new ProductionFlow(_context).FinishQc(CurrentUserId, requestId, result, note, buffer.ToArray());
                SuccessMessage = "Đã trả kết quả và hình ảnh kiểm tra cho Staff.";
            }
            catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnGetImageAsync(int requestId) {
            var check=RequireRole(RoleConstants.Staff,RoleConstants.QCStaff,RoleConstants.WarehouseStaff,RoleConstants.Manager,RoleConstants.Owner);
            if(check!=null)return check;
            var query=_context.ProductionQualityRequests.AsNoTracking().Where(x=>x.RequestId==requestId);
            if(IsStaff)query=query.Where(x=>x.Inspection.RequestedBy==CurrentUserId);
            var item=await query.Select(x=>new{x.ResultImage,x.ResultImageType}).SingleOrDefaultAsync();
            if(item?.ResultImage==null)return NotFound();
            Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers["Cache-Control"]="private, no-store";
            return File(item.ResultImage,item.ResultImageType??"application/octet-stream");
        }
    }
}
