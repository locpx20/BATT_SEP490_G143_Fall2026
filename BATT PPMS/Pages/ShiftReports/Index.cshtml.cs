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
        public int? StaffId { get; set; }
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
        public bool CanModify { get; set; }
        public string? LotCodeGenerated => Status == "Approved" ? $"LOT-{ShiftDate:yyyyMMdd}-{ReportId:D2}" : null;
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

        public bool CanApprove => IsManager;
        public bool IsStaffView => IsStaff;
        public bool CanSubmitReport => IsStaff || IsManager;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // Form properties for submitting a shift report
        [BindProperty] public DateOnly NewShiftDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [BindProperty] public string NewShift { get; set; } = "Ca 1 (06:00 - 14:00)";
        [BindProperty] public int NewRawMaterialUsed { get; set; } = 22000;
        [BindProperty] public int NewProductionQuantity { get; set; } = 20000;
        [BindProperty] public bool NewHasIncident { get; set; }
        [BindProperty] public string? NewIncidentDescription { get; set; }
        [BindProperty] public string? NewNote { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.Staff);
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

            var incidents = await _context.BrokenEquipmentReports
                .Include(b => b.Machine)
                .AsNoTracking().ToListAsync();

            int MaterialUsedOf(EndOfShiftReport r)
            {
                if (!exportByDate.TryGetValue(r.ShiftDate, out var dayTotal)) return 0;
                var dayProduction = productionByDate[r.ShiftDate];
                var share = dayProduction > 0
                    ? (decimal)r.ProductionQuantity / dayProduction
                    : 1m / countByDate[r.ShiftDate];
                return (int)Math.Round(dayTotal * share);
            }

            var visibleReports = IsStaff ? allReports.Where(r => r.StaffId == CurrentUserId) : allReports;
            var items = visibleReports.Select(r =>
            {
                var dayIncidents = incidents
                    .Where(b => r.StaffId.HasValue && b.ReportedBy == r.StaffId && DateOnly.FromDateTime(b.ReportDate) == r.ShiftDate)
                    .ToList();

                return new ShiftReportItemViewModel
                {
                    ReportId = r.ReportId,
                    StaffId = r.StaffId,
                    StaffName = r.Staff?.FullName ?? "Tài khoản đã xóa",
                    ShiftDate = r.ShiftDate,
                    Shift = !string.IsNullOrWhiteSpace(r.Assignment?.Shift) ? r.Assignment!.Shift! : "Ca 1 (06:00 - 14:00)",
                    ProductionQuantity = r.ProductionQuantity,
                    RawMaterialUsed = MaterialUsedOf(r),
                    HasIncident = dayIncidents.Count > 0,
                    IncidentDescription = dayIncidents.Count > 0
                        ? string.Join("; ", dayIncidents.Select(i =>
                        {
                            var machineName = i.Machine != null ? $"{i.Machine.MachineCode} - {i.Machine.MachineName}" : "Thiết bị nhà máy";
                            return string.IsNullOrWhiteSpace(i.Description) ? machineName : $"{machineName}: {i.Description}";
                        }))
                        : null,
                    // Không có cột RejectionReason: lý do từ chối nằm trong dòng gắn thẻ của Note
                    Note = NoteTags.Plain(r.Note),
                    Status = r.Status ?? "Submitted",
                    RejectionReason = NoteTags.Get(r.Note, NoteTags.Rejected),
                    CreatedAt = r.CreatedAt ?? r.ShiftDate.ToDateTime(TimeOnly.MinValue),
                    CanModify = IsStaff && r.StaffId == CurrentUserId && IsEditableStatus(r.Status)
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

        public async Task<IActionResult> OnPostSubmitReportAsync()
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Staff);
            if (check != null) return check;

            if (NewProductionQuantity <= 0 || NewRawMaterialUsed <= 0)
            {
                ErrorMessage = "Sản lượng thực tế và lượng NVL thực dùng phải lớn hơn 0 kg.";
                return RedirectToPage();
            }

            var currentUserId = CurrentUserId > 0 ? CurrentUserId : 4;

            // Tìm phân công ca tương ứng hoặc tạo mới phân công ca
            var assignment = await _context.PersonnelAssignments
                .FirstOrDefaultAsync(p => p.StaffId == currentUserId && p.AssignmentDate == NewShiftDate);

            if (assignment == null)
            {
                assignment = new PersonnelAssignment
                {
                    StaffId = currentUserId,
                    AssignedBy = currentUserId,
                    AssignmentDate = NewShiftDate,
                    Shift = NewShift,
                    TaskDescription = "Vận hành ca sản xuất viên nén",
                    Status = "Completed"
                };
                _context.PersonnelAssignments.Add(assignment);
                await _context.SaveChangesAsync();
            }

            // Ghi nhận lượng NVL thực xuất vào RawMaterialExport
            var firstMaterial = await _context.RawMaterials.FirstOrDefaultAsync();
            if (firstMaterial != null && NewRawMaterialUsed > 0)
            {
                var export = new RawMaterialExport
                {
                    MaterialId = firstMaterial.MaterialId,
                    Quantity = NewRawMaterialUsed,
                    ExportDate = NewShiftDate.ToDateTime(TimeOnly.MinValue),
                    ExportedBy = currentUserId,
                    Note = $"Xuất NVL cho {NewShift}"
                };
                _context.RawMaterialExports.Add(export);
            }

            // Ghi nhận sự cố phát sinh vào BrokenEquipmentReport
            if (NewHasIncident && !string.IsNullOrWhiteSpace(NewIncidentDescription))
            {
                // Dùng máy M-01 (ID=1) làm default khi báo cáo sự cố từ ca trực
                var defaultMachine = await _context.Machines.FirstOrDefaultAsync();
                if (defaultMachine != null)
                {
                    var incident = new BrokenEquipmentReport
                    {
                        MachineId = defaultMachine.MachineId,
                        Description = NewIncidentDescription.Trim(),
                        ReportDate = NewShiftDate.ToDateTime(TimeOnly.MinValue),
                        ReportedBy = currentUserId,
                        Severity = "Medium",
                        Status = "Reported"
                    };
                    _context.BrokenEquipmentReports.Add(incident);
                }
            }

            var report = new EndOfShiftReport
            {
                AssignmentId = assignment.AssignmentId,
                StaffId = currentUserId,
                ShiftDate = NewShiftDate,
                ProductionQuantity = NewProductionQuantity,
                Note = NewNote,
                Status = "Submitted",
                CreatedAt = DateTime.Now
            };

            _context.EndOfShiftReports.Add(report);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Nộp báo cáo ca thành công (Mã: RP-{report.ReportId:D4}). Hồ sơ đã được chuyển cho Quản lý phê duyệt để phát hành mã LOT.";
            return RedirectToPage();
        }

        private static bool IsEditableStatus(string? status) =>
            string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);

        private static string? ShiftNumber(string? shift) => shift?.Trim() switch
        {
            "Ca 1" or "Ca 1 (06:00 - 14:00)" => "Ca 1",
            "Ca 2" or "Ca 2 (14:00 - 22:00)" => "Ca 2",
            "Ca 3" or "Ca 3 (22:00 - 06:00)" => "Ca 3",
            _ => null
        };

        public async Task<IActionResult> OnPostUpdateReportAsync(
            int reportId, DateOnly shiftDate, string? shift, int productionQuantity, string? note)
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;

            var shiftNumber = ShiftNumber(shift);
            var error = InputValidator.Text(note, "Ghi chú ca làm việc", 5000);
            if (!ModelState.IsValid || reportId <= 0 || productionQuantity <= 0 || shiftNumber == null
                || shiftDate.Year < 2000 || shiftDate.Year > 2100 || error != null)
            {
                ErrorMessage = error ?? "Vui lòng nhập ngày, ca hợp lệ và sản lượng lớn hơn 0 kg.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var report = await _context.EndOfShiftReports.Include(r => r.Assignment)
                .SingleOrDefaultAsync(r => r.ReportId == reportId && r.StaffId == CurrentUserId);
            if (report == null)
            {
                ErrorMessage = "Không tìm thấy báo cáo của bạn cần chỉnh sửa.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }
            if (!IsEditableStatus(report.Status))
            {
                ErrorMessage = "Chỉ được chỉnh sửa báo cáo đang chờ duyệt hoặc bị từ chối. Báo cáo đã duyệt được giữ nguyên.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }

            if (report.ShiftDate != shiftDate || ShiftNumber(report.Assignment.Shift) != shiftNumber)
            {
                // Re-link the report; never change the Manager's assignment or another report's shift.
                var assignment = await _context.PersonnelAssignments
                    .Where(p => p.StaffId == CurrentUserId && p.AssignmentDate == shiftDate
                        && (p.Shift == shiftNumber || p.Shift == shift))
                    .OrderBy(p => p.AssignmentId).FirstOrDefaultAsync();
                if (assignment == null)
                {
                    ErrorMessage = "Bạn chưa được phân công vào ngày và ca đã chọn. Vui lòng chọn ca đã có phân công.";
                    return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
                }
                report.Assignment = assignment;
            }
            report.ShiftDate = shiftDate;
            report.ProductionQuantity = productionQuantity;
            report.Note = NoteTags.Plain(InputValidator.Clean(note));
            report.Status = "Submitted";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            SuccessMessage = $"Đã cập nhật báo cáo RP-{reportId:D4} và gửi lại để phê duyệt.";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostDeleteReportAsync(int reportId)
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var report = await _context.EndOfShiftReports
                .SingleOrDefaultAsync(r => r.ReportId == reportId && r.StaffId == CurrentUserId);
            if (report == null)
            {
                ErrorMessage = "Không tìm thấy báo cáo của bạn cần xóa.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }
            if (!IsEditableStatus(report.Status))
            {
                ErrorMessage = "Chỉ được xóa báo cáo đang chờ duyệt hoặc bị từ chối. Báo cáo đã duyệt được giữ nguyên.";
                return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
            }
            // A report does not own the assignment, stock exports or machine incidents.
            _context.EndOfShiftReports.Remove(report);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            SuccessMessage = $"Đã xóa báo cáo RP-{reportId:D4}.";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostApproveReportAsync(int reportId)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

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

            SuccessMessage = $"Đã phê duyệt thành công báo cáo ca RP-{reportId:D4}. Mã LOT: LOT-{report.ShiftDate:yyyyMMdd}-{reportId:D2} đã được phát hành cho phòng KCS.";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostRejectReportAsync(int reportId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

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
