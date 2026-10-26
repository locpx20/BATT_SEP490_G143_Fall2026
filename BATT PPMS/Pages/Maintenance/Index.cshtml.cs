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
        public string EquipmentName { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string Priority { get; set; } = "High";
        public int RequestedByStaffId { get; set; }
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

        public bool CanManage => IsOwner;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff);
            if (check != null) return check;

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Load technicians for assignment dropdown
            AvailableTechnicians = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && (u.Role.RoleName == "Technician" || u.Role.RoleName == "Staff" || u.Role.RoleName == "Manager"))
                .Select(u => new StaffOptionViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    RoleName = u.Role != null ? u.Role.RoleName : "Nhân viên"
                })
                .ToListAsync();

            // Load maintenance requests with navigation properties
            var query = _context.MaintenanceRequests
                .Include(m => m.BrokenEquipmentReport)
                .Include(m => m.Requester)
                .Include(m => m.Assignee)
                .Include(m => m.MaterialPurchaseRequests)
                .AsNoTracking()
                .AsQueryable();

            var all = await query.ToListAsync();

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
                    (r.BrokenEquipmentReport?.EquipmentName != null && r.BrokenEquipmentReport.EquipmentName.ToLowerInvariant().Contains(term)) ||
                    (r.Description != null && r.Description.ToLowerInvariant().Contains(term)) ||
                    (r.Requester?.FullName != null && r.Requester.FullName.ToLowerInvariant().Contains(term)) ||
                    (r.Assignee?.FullName != null && r.Assignee.FullName.ToLowerInvariant().Contains(term))
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
                    EquipmentName = r.BrokenEquipmentReport?.EquipmentName ?? "Thiết bị nhà máy",
                    Symptoms = !string.IsNullOrWhiteSpace(r.Description) ? r.Description : (r.BrokenEquipmentReport?.Description ?? "Chưa có mô tả hiện tượng"),
                    Priority = !string.IsNullOrWhiteSpace(r.Priority) ? r.Priority : (r.BrokenEquipmentReport?.Severity ?? "High"),
                    RequestedByStaffId = r.RequestedBy,
                    RequestedByName = r.Requester?.FullName ?? "Công nhân vận hành",
                    AssignedToStaffId = r.AssignedTo,
                    AssignedStaffName = r.Assignee?.FullName,
                    RequestDate = r.RequestDate,
                    Status = r.Status,
                    CompletionNote = r.CompletionNote,
                    RejectionReason = r.RejectionReason,
                    MaterialRequestsCount = r.MaterialPurchaseRequests?.Count ?? 0
                })
                .ToList();
        }

        public async Task<IActionResult> OnPostAssignStaffAsync(int requestId, int assignedStaffId, string? priority)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền phân công kỹ thuật viên sửa chữa.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var request = await _context.MaintenanceRequests.FindAsync(requestId);
            if (request == null)
            {
                ErrorMessage = "Không tìm thấy yêu cầu sửa chữa cần phân công.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var staff = await _context.Users.FindAsync(assignedStaffId);
            if (staff == null)
            {
                ErrorMessage = "Không tìm thấy nhân viên được phân công.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            request.AssignedTo = assignedStaffId;
            request.Status = "InProgress";
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
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền nghiệm thu hoàn thành sửa chữa máy móc.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            var request = await _context.MaintenanceRequests.FindAsync(requestId);
            if (request == null)
            {
                ErrorMessage = "Không tìm thấy yêu cầu bảo trì.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

            request.Status = "Completed";
            request.CompletionNote = string.IsNullOrWhiteSpace(completionNote) ? "Nghiệm thu máy chạy thử đạt chuẩn, đảm bảo an toàn kỹ thuật." : completionNote.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã nghiệm thu hoàn thành sửa chữa cho yêu cầu REQ-{requestId:D4}. Thiết bị sẵn sàng vận hành.";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostRejectRequestAsync(int requestId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền từ chối yêu cầu sửa chữa.";
                return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
            }

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

            request.Status = "Rejected";
            request.RejectionReason = rejectionReason.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối yêu cầu sửa chữa REQ-{requestId:D4}. Lý do: {request.RejectionReason}";
            return RedirectToPage(new { SearchTerm, PriorityFilter, StatusFilter });
        }
    }
}
