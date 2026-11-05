using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
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
        public decimal RawMaterialUsed { get; set; }
        public List<MaterialUsageInput> MaterialUsage { get; set; } = new();
        public string MaterialUsageSummary { get; set; } = "";
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
        public int? QualityRequestId { get; set; }
        public string? QcResult { get; set; }
        public string? BatchCode { get; set; }
        public string? ReceiptStatus { get; set; }
        public bool CanRequestReceipt { get; set; }
        public string? LotCodeGenerated => BatchCode ?? (Status == "Approved" ? $"LOT-{ShiftDate:yyyyMMdd}-{ReportId:D2}" : null);
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
        public long TotalProductionKg { get; set; }
        public decimal TotalMaterialUsedKg { get; set; }
        public decimal AverageRecoveryRate { get; set; }
        public int PendingApprovalCount { get; set; }

        public bool CanApprove => IsManager;
        public bool IsStaffView => IsStaff;
        public bool CanSubmitReport => IsStaff;
        public List<ProductionQualityRequest> AvailableQcResults { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? QualityRequestId { get; set; }
        [BindProperty] public int NewQualityRequestId { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        [BindProperty] public string? NewNote { get; set; }
        [BindProperty] public List<MaterialUsageInput> MaterialUsage { get; set; } = new();
        public List<RawMaterial> Materials { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.Staff);
            if (check != null) return check;

            await LoadReportsAsync();
            if (IsStaff) {
                Materials = await _context.RawMaterials.AsNoTracking().OrderBy(x => x.MaterialName).ToListAsync();
                AvailableQcResults = await new ProductionFlow(_context).QualityQuery.AsNoTracking()
                    .Where(q => q.Inspection.RequestedBy == CurrentUserId && q.Inspection.Status == "Completed")
                    .OrderByDescending(q => q.RequestId).ToListAsync();
                NewQualityRequestId = AvailableQcResults.FirstOrDefault(q => q.RequestId == QualityRequestId && q.ReportId == null)?.RequestId ?? 0;
            }
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
            var usageRows = await _context.ShiftMaterialUsages.Include(x => x.Material).AsNoTracking().ToListAsync();
            var exports = await _context.RawMaterialExports.AsNoTracking().ToListAsync();
            var materialRequests = await _context.ProductionMaterialRequests.Include(x => x.Assignment)
                .AsNoTracking().Where(x => x.Status == "Issued").ToListAsync();
            var workflowExportIds = materialRequests.Where(x => x.ExportId.HasValue).Select(x => x.ExportId!.Value).ToHashSet();
            workflowExportIds.UnionWith(usageRows.Where(x => x.ExportId.HasValue).Select(x => x.ExportId!.Value));
            var exportByDate = exports
                .Where(x => !workflowExportIds.Contains(x.ExportId))
                .GroupBy(e => DateOnly.FromDateTime(e.ExportDate))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));

            var productionByDate = allReports
                .GroupBy(r => r.ShiftDate)
                .ToDictionary(g => g.Key, g => g.Sum(r => (long)r.ProductionQuantity));
            var countByDate = allReports
                .GroupBy(r => r.ShiftDate)
                .ToDictionary(g => g.Key, g => g.Count());

            var incidents = await _context.BrokenEquipmentReports
                .Include(b => b.Machine)
                .AsNoTracking().ToListAsync();

            var qualityByReport = await new ProductionFlow(_context).QualityQuery.AsNoTracking()
                .Where(q => q.ReportId != null).ToDictionaryAsync(q => q.ReportId!.Value);
            decimal MaterialUsedOf(EndOfShiftReport r)
            {
                var recorded = usageRows.Where(x => x.ReportId == r.ReportId).ToList();
                if (recorded.Count > 0) return recorded.Where(x => x.Material.Unit.Trim().Equals("kg", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
                if (qualityByReport.ContainsKey(r.ReportId)) {
                    var issued = materialRequests.Where(x => x.RequestedBy == r.StaffId && x.Assignment.AssignmentDate == r.ShiftDate
                        && x.Assignment.Shift == r.Assignment.Shift).Sum(x => x.Quantity);
                    var caProduction = allReports.Where(x => qualityByReport.ContainsKey(x.ReportId) && x.StaffId == r.StaffId && x.ShiftDate == r.ShiftDate
                        && x.Assignment.Shift == r.Assignment.Shift).Sum(x => (long)x.ProductionQuantity);
                    return caProduction > 0 ? Math.Round(issued * r.ProductionQuantity / caProduction, 2) : 0;
                }
                if (!exportByDate.TryGetValue(r.ShiftDate, out var dayTotal)) return 0;
                var dayProduction = productionByDate[r.ShiftDate];
                var share = dayProduction > 0
                    ? (decimal)r.ProductionQuantity / dayProduction
                    : 1m / countByDate[r.ShiftDate];
                return Math.Round(dayTotal * share, 2);
            }

            var visibleReports = IsStaff ? allReports.Where(r => r.StaffId == CurrentUserId) : allReports;
            var items = visibleReports.Select(r =>
            {
                var dayIncidents = incidents
                    .Where(b => r.StaffId.HasValue && b.ReportedBy == r.StaffId && DateOnly.FromDateTime(b.ReportDate) == r.ShiftDate)
                    .ToList();

                qualityByReport.TryGetValue(r.ReportId, out var quality);
                return new ShiftReportItemViewModel
                {
                    ReportId = r.ReportId,
                    QualityRequestId = quality?.RequestId,
                    QcResult = quality?.Inspection.Result,
                    BatchCode = quality?.Inspection.Batch.BagNumber,
                    ReceiptStatus = quality?.Receipt?.Status,
                    CanRequestReceipt = IsStaff && r.StaffId == CurrentUserId && r.Status == "Approved" && quality?.Inspection.Result == "Pass"
                        && (quality.Receipt == null || quality.Receipt.Status == "Rejected"),
                    StaffId = r.StaffId,
                    StaffName = r.Staff?.FullName ?? "Tài khoản đã xóa",
                    ShiftDate = r.ShiftDate,
                    Shift = !string.IsNullOrWhiteSpace(r.Assignment?.Shift) ? r.Assignment!.Shift! : "Ca 1 (06:00 - 14:00)",
                    ProductionQuantity = r.ProductionQuantity,
                    RawMaterialUsed = MaterialUsedOf(r),
                    MaterialUsage = usageRows.Where(x => x.ReportId == r.ReportId).Select(x => new MaterialUsageInput { MaterialId = x.MaterialId, Quantity = x.Quantity }).ToList(),
                    MaterialUsageSummary = string.Join("; ", usageRows.Where(x => x.ReportId == r.ReportId).Select(x => $"{x.Material.MaterialName}: {x.Quantity:N2} {x.Material.Unit}")),
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
            TotalProductionKg = items.Sum(r => (long)r.ProductionQuantity);
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
            var check = RequireRole(RoleConstants.Staff); if (check != null) return check;
            if (!ModelState.IsValid) { ErrorMessage = "Vui lòng chọn kết quả QC hợp lệ."; return RedirectToPage(); }
            try {
                var id = await new ProductionFlow(_context).SubmitReport(CurrentUserId, NewQualityRequestId, NewNote, MaterialUsage);
                SuccessMessage = $"Đã gửi báo cáo RP-{id:D4} kèm kết quả QC cho Manager phê duyệt.";
            } catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage();
        }

        private static bool IsEditableStatus(string? status) =>
            string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);

        public async Task<IActionResult> OnPostUpdateReportAsync(int reportId, int qualityRequestId, string? note)
        {
            var check = RequireRole(RoleConstants.Staff); if (check != null) return check;
            if (!ModelState.IsValid) { ErrorMessage = "Thông tin báo cáo không hợp lệ."; return RedirectToPage(); }
            try {
                await new ProductionFlow(_context).UpdateReport(CurrentUserId, reportId, qualityRequestId, note, MaterialUsage);
                SuccessMessage = $"Đã cập nhật báo cáo RP-{reportId:D4} và gửi lại Manager.";
            } catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
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
            var quality = await _context.ProductionQualityRequests.SingleOrDefaultAsync(q => q.ReportId == reportId);
            if (quality != null) {
                quality.ReportId = null;
                await _context.SaveChangesAsync();
            }
            var usages = await _context.ShiftMaterialUsages.Where(x => x.ReportId == reportId).ToListAsync();
            if (usages.Any(x => x.ExportId.HasValue)) { ErrorMessage = "Báo cáo đã trừ tồn không được xóa."; return RedirectToPage(); }
            _context.ShiftMaterialUsages.RemoveRange(usages);
            // Keep QC results, assignments, stock exports and incidents when deleting reports.
            _context.EndOfShiftReports.Remove(report);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            SuccessMessage = $"Đã xóa báo cáo RP-{reportId:D4}.";
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostApproveReportAsync(int reportId)
        {
            var check = RequireRole(RoleConstants.Manager); if (check != null) return check;
            try {
                await new ProductionFlow(_context).ApproveReport(CurrentUserId, reportId);
                SuccessMessage = $"Đã duyệt báo cáo RP-{reportId:D4}. Staff có thể yêu cầu nhập kho nếu lô QC đạt.";
            } catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostRejectReportAsync(int reportId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Manager); if (check != null) return check;
            try {
                await new ProductionFlow(_context).RejectReport(CurrentUserId, reportId, rejectionReason);
                SuccessMessage = $"Đã trả lại báo cáo RP-{reportId:D4} kèm lý do cho Staff.";
            } catch (ProductionFlowException ex) { ErrorMessage = ex.Message; }
            return RedirectToPage(new { SearchTerm, ShiftFilter, StatusFilter });
        }
    }
}
