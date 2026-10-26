using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.ShiftReports
{
    public class ShiftReportItemViewModel
    {
        public int ReportId { get; set; }
        public string Code => $"RP-{ReportId:D4}";
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public DateOnly ShiftDate { get; set; }
        public string Shift { get; set; } = "Ca 1 (06:00 - 14:00)";
        public int ProductionQuantity { get; set; }
        public int RawMaterialUsed { get; set; }
        public decimal RecoveryRate => RawMaterialUsed > 0 
            ? Math.Round((decimal)ProductionQuantity / RawMaterialUsed * 100m, 1) 
            : 0m;
        public bool HasIncident { get; set; }
        public string? IncidentDescription { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = "Submitted";
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ShiftFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public List<ShiftReportItemViewModel> Reports { get; set; } = new();

        // KPI Summary
        public int TotalProductionKg { get; set; }
        public int TotalMaterialUsedKg { get; set; }
        public decimal AverageRecoveryRate { get; set; }
        public int PendingApprovalCount { get; set; }

        public bool CanApprove => IsOwner;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            await LoadReportsAsync();
            return Page();
        }

        private async Task LoadReportsAsync()
        {
            var query = _context.EndOfShiftReports
                .Include(r => r.Staff)
                .AsNoTracking()
                .AsQueryable();

            var allReports = await query.ToListAsync();

            // Calculate overall metrics
            TotalProductionKg = allReports.Sum(r => r.ProductionQuantity);
            TotalMaterialUsedKg = allReports.Sum(r => r.RawMaterialUsed ?? 0);
            AverageRecoveryRate = TotalMaterialUsedKg > 0 
                ? Math.Round((decimal)TotalProductionKg / TotalMaterialUsedKg * 100m, 1) 
                : 0m;
            PendingApprovalCount = allReports.Count(r => string.Equals(r.Status, "Submitted", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));

            // Filtering
            var filtered = allReports.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(r => 
                    $"RP-{r.ReportId:D4}".ToLowerInvariant().Contains(term) ||
                    (r.Staff?.FullName != null && r.Staff.FullName.ToLowerInvariant().Contains(term)) ||
                    (r.Note != null && r.Note.ToLowerInvariant().Contains(term)) ||
                    (r.IncidentDescription != null && r.IncidentDescription.ToLowerInvariant().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(ShiftFilter) && ShiftFilter != "ALL")
            {
                filtered = filtered.Where(r => (r.Shift ?? "").Contains(ShiftFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                filtered = filtered.Where(r => string.Equals(r.Status, StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            Reports = filtered
                .OrderByDescending(r => r.ShiftDate)
                .ThenByDescending(r => r.ReportId)
                .Select(r => new ShiftReportItemViewModel
                {
                    ReportId = r.ReportId,
                    StaffId = r.StaffId,
                    StaffName = r.Staff?.FullName ?? "Chưa xác định",
                    ShiftDate = r.ShiftDate,
                    Shift = !string.IsNullOrWhiteSpace(r.Shift) ? r.Shift : "Ca 1 (06:00 - 14:00)",
                    ProductionQuantity = r.ProductionQuantity,
                    RawMaterialUsed = r.RawMaterialUsed ?? 0,
                    HasIncident = r.HasIncident ?? false,
                    IncidentDescription = r.IncidentDescription,
                    Note = r.Note,
                    Status = r.Status ?? "Submitted",
                    RejectionReason = r.RejectionReason,
                    CreatedAt = r.CreatedAt
                })
                .ToList();
        }

        public async Task<IActionResult> OnPostApproveReportAsync(int reportId)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền phê duyệt báo cáo ca sản xuất.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            var report = await _context.EndOfShiftReports.FindAsync(reportId);
            if (report == null)
            {
                ErrorMessage = "Không tìm thấy báo cáo ca cần duyệt.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            report.Status = "Approved";
            report.RejectionReason = null;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã phê duyệt thành công báo cáo ca RP-{reportId:D4}.";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostRejectReportAsync(int reportId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền từ chối báo cáo ca sản xuất.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            var report = await _context.EndOfShiftReports.FindAsync(reportId);
            if (report == null)
            {
                ErrorMessage = "Không tìm thấy báo cáo ca.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối báo cáo ca.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            report.Status = "Rejected";
            report.RejectionReason = rejectionReason.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối báo cáo ca RP-{reportId:D4}. Lý do: {report.RejectionReason}";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }
    }
}
