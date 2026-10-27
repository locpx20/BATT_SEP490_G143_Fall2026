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
            var allReports = await _context.EndOfShiftReports
                .Include(r => r.Staff)
                .Include(r => r.Assignment)
                .AsNoTracking()
                .ToListAsync();

            // Schema mới không còn RawMaterialUsed / HasIncident / IncidentDescription trong EndOfShiftReport:
            //  - NVL tiêu hao: lấy từ RawMaterialExport theo ngày ca, chia cho các báo cáo trong ngày theo tỷ lệ sản lượng
            //  - Sự cố: lấy từ BrokenEquipmentReport do chính nhân viên đó báo trong ngày ca
            var exports = await _context.RawMaterialExports.AsNoTracking().ToListAsync();
            var exportByDate = exports
                .GroupBy(e => DateOnly.FromDateTime(e.ExportDate))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));

            var productionByDate = allReports
                .GroupBy(r => r.ShiftDate)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.ProductionQuantity));
            var countByDate = allReports
                .GroupBy(r => r.ShiftDate)
                .ToDictionary(g => g.Key, g => g.Count());

            var incidents = await _context.BrokenEquipmentReports.AsNoTracking().ToListAsync();

            int MaterialUsedOf(EndOfShiftReport r)
            {
                if (!exportByDate.TryGetValue(r.ShiftDate, out var dayTotal)) return 0;
                var dayProduction = productionByDate[r.ShiftDate];
                var share = dayProduction > 0
                    ? (decimal)r.ProductionQuantity / dayProduction
                    : 1m / countByDate[r.ShiftDate];
                return (int)Math.Round(dayTotal * share);
            }

            var items = allReports.Select(r =>
            {
                var dayIncidents = incidents
                    .Where(b => b.ReportedBy == r.StaffId && DateOnly.FromDateTime(b.ReportDate) == r.ShiftDate)
                    .ToList();

                return new ShiftReportItemViewModel
                {
                    ReportId = r.ReportId,
                    StaffId = r.StaffId,
                    StaffName = r.Staff?.FullName ?? "Chưa xác định",
                    ShiftDate = r.ShiftDate,
                    Shift = !string.IsNullOrWhiteSpace(r.Assignment?.Shift) ? r.Assignment!.Shift! : "Ca 1 (06:00 - 14:00)",
                    ProductionQuantity = r.ProductionQuantity,
                    RawMaterialUsed = MaterialUsedOf(r),
                    HasIncident = dayIncidents.Count > 0,
                    IncidentDescription = dayIncidents.Count > 0
                        ? string.Join("; ", dayIncidents.Select(i => string.IsNullOrWhiteSpace(i.Description) ? i.EquipmentName : $"{i.EquipmentName}: {i.Description}"))
                        : null,
                    // Không có cột RejectionReason: lý do từ chối nằm trong dòng gắn thẻ của Note
                    Note = NoteTags.Plain(r.Note),
                    Status = r.Status ?? "Submitted",
                    RejectionReason = NoteTags.Get(r.Note, NoteTags.Rejected),
                    CreatedAt = r.CreatedAt ?? r.ShiftDate.ToDateTime(TimeOnly.MinValue)
                };
            }).ToList();

            // Calculate overall metrics
            TotalProductionKg = items.Sum(r => r.ProductionQuantity);
            TotalMaterialUsedKg = items.Sum(r => r.RawMaterialUsed);
            AverageRecoveryRate = TotalMaterialUsedKg > 0
                ? Math.Round((decimal)TotalProductionKg / TotalMaterialUsedKg * 100m, 1)
                : 0m;
            PendingApprovalCount = items.Count(r => string.Equals(r.Status, "Submitted", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase));

            // Filtering
            var filtered = items.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(r =>
                    r.Code.ToLowerInvariant().Contains(term) ||
                    r.StaffName.ToLowerInvariant().Contains(term) ||
                    (r.Note != null && r.Note.ToLowerInvariant().Contains(term)) ||
                    (r.IncidentDescription != null && r.IncidentDescription.ToLowerInvariant().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(ShiftFilter) && ShiftFilter != "ALL")
            {
                filtered = filtered.Where(r => r.Shift.Contains(ShiftFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                filtered = filtered.Where(r => string.Equals(r.Status, StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            Reports = filtered
                .OrderByDescending(r => r.ShiftDate)
                .ThenByDescending(r => r.ReportId)
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

            if (report.Status == "Approved")
            {
                ErrorMessage = $"Báo cáo ca RP-{reportId:D4} đã được phê duyệt trước đó.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            report.Status = "Approved";
            report.Note = NoteTags.Remove(report.Note, NoteTags.Rejected);
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

            var reasonError = InputValidator.Text(rejectionReason, "Lý do từ chối", 500);
            if (reasonError != null)
            {
                ErrorMessage = reasonError;
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            if (report.Status == "Approved")
            {
                ErrorMessage = "Không thể từ chối báo cáo ca đã được phê duyệt.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            report.Status = "Rejected";
            report.Note = NoteTags.Set(report.Note, NoteTags.Rejected, rejectionReason.Trim());
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối báo cáo ca RP-{reportId:D4}. Lý do: {rejectionReason.Trim()}";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }
    }
}
