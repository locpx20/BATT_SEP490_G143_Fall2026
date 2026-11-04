using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Maintenance
{
    public class RepairRequestItemViewModel
    {
        public int RequestId { get; set; }
        public string Code => $"REQ-{RequestId:D4}";
        public int BrokenReportId { get; set; }
        public int MachineId { get; set; }
        public bool CanModify { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string Priority { get; set; } = "High";
        public int? RequestedByStaffId { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public int? AssignedToStaffId { get; set; }
        public string? AssignedStaffName { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string? CompletionNote { get; set; }
        public string? RejectionReason { get; set; }
        public int MaterialRequestsCount { get; set; }
    }

    public class StaffOptionViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
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
        public string? PriorityFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public List<RepairRequestItemViewModel> Requests { get; set; } = new();
        public List<StaffOptionViewModel> AvailableTechnicians { get; set; } = new();

        // KPI Summary
        public int TotalRequests { get; set; }
        public int PendingAssignCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }

        public bool CanManage => IsManager;
        public bool CanReportFailure => IsStaff || IsManager;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        [BindProperty] public int FailureMachineId { get; set; }
        [BindProperty] public string FailureSymptoms { get; set; } = string.Empty;
        [BindProperty] public string FailureSeverity { get; set; } = "High";
        [BindProperty] public string? FailureDescription { get; set; }
        [BindProperty] public int FailureRequestId { get; set; }

        public List<Machine> AvailableMachines { get; set; } = new();
        public bool IsStaffView => IsStaff;

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff, RoleConstants.Staff);
            if (check != null) return check;

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Load technicians for assignment dropdown
            AvailableTechnicians = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == RoleConstants.MaintenanceStaff && u.Status == StatusConstants.Active)
                .Select(u => new StaffOptionViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    RoleName = u.Role != null ? u.Role.RoleName : "Nhân viên"
                })
                .ToListAsync();

            // Load available machines for dropdown
            AvailableMachines = await _context.Machines.OrderBy(m => m.MachineCode).ToListAsync();

            // Load maintenance requests with navigation properties
            var query = _context.MaintenanceRequests
                .Include(m => m.BrokenReport)
                    .ThenInclude(b => b.Machine)
                .Include(m => m.RequestedByNavigation)
                .Include(m => m.AssignedToNavigation)
                .Include(m => m.MaterialPurchaseRequests)
                .AsNoTracking()
                .AsQueryable();

            if (IsStaff)
                query = query.Where(r => r.RequestedBy == CurrentUserId || r.BrokenReport.ReportedBy == CurrentUserId);

            var all = await query.ToListAsync();
            var reportIds = all.Select(r => r.BrokenReportId).ToList();
            var sharedReports = (await _context.MaintenanceRequests
                .Where(r => reportIds.Contains(r.BrokenReportId))
                .GroupBy(r => r.BrokenReportId).Where(g => g.Count() > 1)
                .Select(g => g.Key).ToListAsync()).ToHashSet();

            // KPIs
            TotalRequests = all.Count;
            PendingAssignCount = all.Count(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "Reported", StringComparison.OrdinalIgnoreCase));
            InProgressCount = all.Count(r => string.Equals(r.Status, "InProgress", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "Assigned", StringComparison.OrdinalIgnoreCase));
            CompletedCount = all.Count(r => string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            // Filtering
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(r =>
                    $"REQ-{r.RequestId:D4}".ToLowerInvariant().Contains(term) ||
                    (r.BrokenReport?.Machine?.MachineName != null && r.BrokenReport.Machine.MachineName.ToLowerInvariant().Contains(term)) ||
                    (r.BrokenReport?.Machine?.MachineCode != null && r.BrokenReport.Machine.MachineCode.ToLowerInvariant().Contains(term)) ||
                    (NoteTags.Plain(r.Description) != null && NoteTags.Plain(r.Description)!.ToLowerInvariant().Contains(term)) ||
                    (r.RequestedByNavigation?.FullName != null && r.RequestedByNavigation.FullName.ToLowerInvariant().Contains(term)) ||
                    (r.AssignedToNavigation?.FullName != null && r.AssignedToNavigation.FullName.ToLowerInvariant().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(PriorityFilter) && PriorityFilter != "ALL")
            {
                filtered = filtered.Where(r => string.Equals(r.Priority, PriorityFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                filtered = filtered.Where(r => string.Equals(r.Status, StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            Requests = filtered
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.RequestId)
                .Select(r => new RepairRequestItemViewModel
                {
                    RequestId = r.RequestId,
                    BrokenReportId = r.BrokenReportId,
                    MachineId = r.BrokenReport.MachineId,
                    CanModify = IsStaff && CanModifyFailure(r) && !sharedReports.Contains(r.BrokenReportId),
                    EquipmentName = r.BrokenReport?.Machine != null
                        ? $"{r.BrokenReport.Machine.MachineCode} - {r.BrokenReport.Machine.MachineName}"
                        : "Thiết bị nhà máy",
                    Symptoms = !string.IsNullOrWhiteSpace(NoteTags.Plain(r.Description)) ? NoteTags.Plain(r.Description)! : (r.BrokenReport?.Description ?? "Chưa có mô tả hiện tượng"),
                    Priority = !string.IsNullOrWhiteSpace(r.Priority) ? r.Priority : (r.BrokenReport?.Severity ?? "High"),
                    RequestedByStaffId = r.RequestedBy,
                    RequestedByName = r.RequestedByNavigation?.FullName ?? "Tài khoản đã xóa",
                    AssignedToStaffId = r.AssignedTo,
                    AssignedStaffName = r.AssignedToNavigation?.FullName,
                    RequestDate = r.RequestDate,
                    Status = r.Status ?? "Pending",
                    // Schema mới không có cột CompletionNote / RejectionReason: đọc từ dòng gắn thẻ trong Description
                    CompletionNote = NoteTags.Get(r.Description, NoteTags.Completed),
                    RejectionReason = NoteTags.Get(r.Description, NoteTags.Rejected),
                    MaterialRequestsCount = r.MaterialPurchaseRequests?.Count ?? 0
                })
                .ToList();
        }

        public async Task<IActionResult> OnPostAssignStaffAsync(int requestId, int assignedStaffId, string? priority)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var request = await _context.MaintenanceRequests.FindAsync(requestId);
            if (request == null)
            {
                ErrorMessage = "Không tìm thấy yêu cầu sửa chữa cần phân công.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.Status, "Reported", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = $"Yêu cầu REQ-{requestId:D4} đang ở trạng thái '{request.Status}', không thể phân công.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var priorityError = InputValidator.Status(priority, StatusSets.PriorityLevels, "Độ ưu tiên");
            if (priorityError != null)
            {
                ErrorMessage = priorityError;
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var staff = await _context.Users.SingleOrDefaultAsync(u => u.UserId == assignedStaffId
                && u.Status == StatusConstants.Active && u.Role.RoleName == RoleConstants.MaintenanceStaff);
            if (staff == null)
            {
                ErrorMessage = "Vui lòng chọn nhân viên bảo trì có tài khoản đang hoạt động.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            request.AssignedTo = assignedStaffId;
            request.Status = "InProgress";
            request.Description = NoteTags.Remove(request.Description, NoteTags.Rejected);
            if (!string.IsNullOrWhiteSpace(priority))
            {
                request.Priority = priority;
            }
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã tiếp nhận và phân công yêu cầu REQ-{requestId:D4} cho kỹ thuật viên {staff.FullName}.";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostCompleteRequestAsync(int requestId, string completionNote)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var request = await _context.MaintenanceRequests.FindAsync(requestId);
            if (request == null)
            {
                ErrorMessage = "Không tìm thấy yêu cầu bảo trì.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            if (!string.Equals(request.Status, "InProgress", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.Status, "In Progress", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = $"Chỉ nghiệm thu được yêu cầu đang sửa chữa (hiện tại: '{request.Status}').";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var noteError = InputValidator.Text(completionNote, "Ghi chú nghiệm thu", 500);
            if (noteError != null)
            {
                ErrorMessage = noteError;
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            request.Status = "Completed";
            request.Description = NoteTags.Set(request.Description, NoteTags.Completed,
                string.IsNullOrWhiteSpace(completionNote) ? "Nghiệm thu máy chạy thử đạt chuẩn, đảm bảo an toàn kỹ thuật." : completionNote.Trim());
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã nghiệm thu hoàn thành sửa chữa cho yêu cầu REQ-{requestId:D4}. Thiết bị sẵn sàng vận hành.";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostRejectRequestAsync(int requestId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var request = await _context.MaintenanceRequests.FindAsync(requestId);
            if (request == null)
            {
                ErrorMessage = "Không tìm thấy yêu cầu sửa chữa.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối yêu cầu sửa chữa.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var reasonError = InputValidator.Text(rejectionReason, "Lý do từ chối", 500);
            if (reasonError != null)
            {
                ErrorMessage = reasonError;
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            if (string.Equals(request.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Không thể từ chối yêu cầu đã hoàn thành.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            request.Status = "Rejected";
            request.Description = NoteTags.Set(request.Description, NoteTags.Rejected, rejectionReason.Trim());
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối yêu cầu sửa chữa REQ-{requestId:D4}. Lý do: {rejectionReason.Trim()}";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }

        private bool CanModifyFailure(MaintenanceRequest request) =>
            request.RequestedBy == CurrentUserId && request.BrokenReport.ReportedBy == CurrentUserId
            && request.AssignedTo == null && request.MaterialPurchaseRequests.Count == 0
            && string.Equals(request.BrokenReport.Status, "Reported", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.Status, "Reported", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase));

        public async Task<IActionResult> OnPostUpdateFailureAsync()
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;
            var description = NoteTags.Plain(InputValidator.Clean(FailureSymptoms));
            var extra = NoteTags.Plain(InputValidator.Clean(FailureDescription));
            if (extra != null) description += $" | Ghi chú: {extra}";
            var error = InputValidator.First(
                InputValidator.Text(description, "Mô tả sự cố", 10000, required: true),
                InputValidator.Status(FailureSeverity, StatusSets.PriorityLevels, "Mức độ"));
            if (!ModelState.IsValid || FailureRequestId <= 0 || FailureMachineId <= 0
                || string.IsNullOrWhiteSpace(FailureSymptoms) || string.IsNullOrWhiteSpace(FailureSeverity) || error != null)
            {
                ErrorMessage = error ?? "Vui lòng chọn máy móc, mức độ và nhập hiện tượng sự cố hợp lệ.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var request = await _context.MaintenanceRequests.Include(r => r.BrokenReport)
                .Include(r => r.MaterialPurchaseRequests)
                .SingleOrDefaultAsync(r => r.RequestId == FailureRequestId && r.RequestedBy == CurrentUserId);
            if (request == null || !CanModifyFailure(request)
                || await _context.MaintenanceRequests.CountAsync(r => r.BrokenReportId == request.BrokenReportId) != 1)
            {
                ErrorMessage = "Chỉ được sửa báo cáo của mình còn chờ tiếp nhận hoặc bị từ chối, chưa được phân công hay liên kết xử lý.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }
            if (!await _context.Machines.AnyAsync(m => m.MachineId == FailureMachineId))
            {
                ErrorMessage = "Không tìm thấy máy móc đã chọn.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }
            request.BrokenReport.MachineId = FailureMachineId;
            request.BrokenReport.Description = description;
            request.BrokenReport.Severity = FailureSeverity.Trim();
            request.Description = description;
            request.Priority = FailureSeverity.Trim();
            request.Status = "Pending";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            SuccessMessage = $"Đã cập nhật báo cáo REQ-{FailureRequestId:D4} và gửi lại để tiếp nhận.";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostDeleteFailureAsync(int requestId)
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var request = await _context.MaintenanceRequests.Include(r => r.BrokenReport)
                .Include(r => r.MaterialPurchaseRequests)
                .SingleOrDefaultAsync(r => r.RequestId == requestId && r.RequestedBy == CurrentUserId);
            if (request == null || !CanModifyFailure(request)
                || await _context.MaintenanceRequests.CountAsync(r => r.BrokenReportId == request.BrokenReportId) != 1)
            {
                ErrorMessage = "Chỉ được xóa báo cáo của mình còn chờ tiếp nhận hoặc bị từ chối, chưa được phân công hay liên kết xử lý.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }
            _context.MaintenanceRequests.Remove(request);
            _context.BrokenEquipmentReports.Remove(request.BrokenReport);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            SuccessMessage = $"Đã xóa báo cáo sự cố REQ-{requestId:D4}.";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostReportFailureAsync()
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Staff, RoleConstants.MaintenanceStaff);
            if (check != null) return check;

            if (FailureMachineId <= 0 || string.IsNullOrWhiteSpace(FailureSymptoms))
            {
                ErrorMessage = "Vui lòng chọn thiết bị bị hỏng và mô tả hiện tượng sự cố.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var currentUserId = CurrentUserId > 0 ? CurrentUserId : 4;

            var machine = await _context.Machines.FindAsync(FailureMachineId);
            if (machine == null)
            {
                ErrorMessage = "Không tìm thấy thiết bị được chọn. Vui lòng thử lại.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var report = new BrokenEquipmentReport
            {
                MachineId = FailureMachineId,
                Description = FailureSymptoms.Trim() + (!string.IsNullOrWhiteSpace(FailureDescription) ? $" | Ghi chú: {FailureDescription.Trim()}" : ""),
                ReportedBy = currentUserId,
                ReportDate = DateTime.Now,
                Status = "Reported",
                Severity = FailureSeverity
            };

            _context.BrokenEquipmentReports.Add(report);
            await _context.SaveChangesAsync();

            var req = new MaintenanceRequest
            {
                BrokenReportId = report.ReportId,
                RequestedBy = currentUserId,
                RequestDate = DateTime.Now,
                Status = "Pending",
                Priority = FailureSeverity,
                Description = report.Description
            };

            _context.MaintenanceRequests.Add(req);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Báo cáo sự cố thành công cho thiết bị '{machine.MachineCode} - {machine.MachineName}' (Mã: REQ-{req.RequestId:D4}). Kỹ thuật viên sẽ được tiếp nhận và xử lý.";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }
    }
}
