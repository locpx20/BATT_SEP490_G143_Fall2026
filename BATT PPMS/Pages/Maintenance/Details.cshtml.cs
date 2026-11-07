using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Maintenance
{
    public class TimelineItem
    {
        public DateTime At { get; set; }
        public string Action { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string ActorRole { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class TechnicianOption
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    /// <summary>
    /// Chi tiết yêu cầu sửa chữa và toàn bộ thao tác theo vai trò:
    /// Manager duyệt/từ chối/phân công, Maintenance Staff xác nhận/hoàn thành, Staff sửa lại/hủy.
    /// </summary>
    public class DetailsModel : BasePageModel
    {
        private const int AssignNoteMax = 300;
        private const int MaterialQtyMax = 9999;
        private const int MaterialLinesMax = 20;

        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? WarningMessage { get; set; }

        public MaintenanceRequest Item { get; private set; } = null!;
        public string Status { get; private set; } = RepairStatus.Pending;
        public string Title { get; private set; } = string.Empty;
        public string? PlainDescription { get; private set; }
        public string? RejectionReason { get; private set; }
        public string? CompletionNote { get; private set; }
        public List<TimelineItem> Timeline { get; private set; } = new();
        public List<TechnicianOption> Technicians { get; private set; } = new();

        // Vật tư sửa chữa
        public List<MaterialPurchaseRequest> PurchaseRequests { get; private set; } = new();
        public List<AppNotification> Notices { get; private set; } = new();
        public bool HasPendingPurchase => PurchaseRequests.Any(p => PurchaseRequestStatus.Normalize(p.Status) == PurchaseRequestStatus.Pending);

        // Quyền thao tác (tính theo vai trò + trạng thái hiện tại)
        public bool CanReview { get; private set; }       // Manager: duyệt + phân công / từ chối
        public bool CanReassign { get; private set; }     // Manager: phân công lại khi chưa xác nhận
        public bool CanConfirm { get; private set; }      // Maintenance Staff được giao: xác nhận nhận việc
        public bool CanComplete { get; private set; }     // Maintenance Staff được giao: hoàn thành
        public bool CanEditResubmit { get; private set; } // Staff tạo: sửa và gửi lại khi bị từ chối
        public bool CanCancel { get; private set; }       // Staff tạo: hủy khi chờ duyệt / bị từ chối
        public bool CanRequestMaterial { get; private set; } // Maintenance Staff được giao: thiếu vật tư thì tạo yêu cầu mua

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff, RoleConstants.Staff);
            if (check != null) return check;

            var request = await _context.MaintenanceRequests
                .AsNoTracking()
                .Include(r => r.BrokenReport).ThenInclude(b => b.Machine)
                .Include(r => r.RequestedByNavigation)
                .Include(r => r.AssignedToNavigation)
                .Include(r => r.ReviewedByNavigation)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            // Staff chỉ xem yêu cầu của mình, Maintenance Staff chỉ xem việc được giao
            if (request == null || !CanView(request))
            {
                ErrorMessage = "Không tìm thấy yêu cầu sửa chữa hoặc bạn không có quyền xem yêu cầu này.";
                return RedirectToPage("/Maintenance/Index");
            }

            Item = request;
            Status = RepairStatus.Normalize(request.Status);
            Title = IndexModel.DisplayTitle(request.Title, request.Description);
            PlainDescription = NoteTags.Plain(request.Description);
            RejectionReason = request.RejectionReason ?? NoteTags.Get(request.Description, NoteTags.Rejected);
            CompletionNote = request.CompletionNote ?? NoteTags.Get(request.Description, NoteTags.Completed);

            var mine = request.AssignedTo == CurrentUserId;
            var owner = request.RequestedBy == CurrentUserId;
            CanReview = IsManager && Status == RepairStatus.Pending;
            CanReassign = IsManager && Status == RepairStatus.Assigned;
            CanConfirm = IsMaintenanceStaff && mine && Status == RepairStatus.Assigned;
            CanRequestMaterial = IsMaintenanceStaff && mine && Status == RepairStatus.InProgress;
            CanComplete = IsMaintenanceStaff && mine && Status == RepairStatus.InProgress;
            CanEditResubmit = IsStaff && owner && Status == RepairStatus.Rejected;
            CanCancel = IsStaff && owner && (Status == RepairStatus.Pending || Status == RepairStatus.Rejected);

            if (CanReview || CanReassign)
            {
                Technicians = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Status == StatusConstants.Active && u.Role != null && u.Role.RoleName == RoleConstants.MaintenanceStaff)
                    .OrderBy(u => u.FullName)
                    .Select(u => new TechnicianOption { UserId = u.UserId, FullName = u.FullName, Phone = u.Phone })
                    .ToListAsync();
            }

            await LoadPurchaseRequestsAsync(id);

            Notices = await Notifier.TakeUnreadAsync(_context, CurrentUserId);

            Timeline = await _context.MaintenanceRequestLogs
                .AsNoTracking()
                .Where(l => l.RequestId == id)
                .OrderBy(l => l.CreatedAt).ThenBy(l => l.LogId)
                .Select(l => new TimelineItem
                {
                    At = l.CreatedAt,
                    Action = l.Action,
                    ActorName = l.Actor.FullName,
                    ActorRole = l.Actor.Role != null ? l.Actor.Role.RoleName : string.Empty,
                    Note = l.Note
                })
                .ToListAsync();

            return Page();
        }

        // ------------------------------------------------------------------
        // Manager: phê duyệt + phân công cho Maintenance Staff
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostApproveAsync(int id, int assignedStaffId, string? priority, DateTime? dueDate, string? note)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null) return MissingRequest();

            if (RepairStatus.Normalize(request.Status) != RepairStatus.Pending)
                return Fail(id, $"Yêu cầu REQ-{id:D4} không còn ở trạng thái chờ duyệt.");

            var (staff, error) = await ValidateAssignmentAsync(assignedStaffId, priority, dueDate, note);
            if (error != null) return Fail(id, error);

            var now = DateTime.Now;
            ApplyAssignment(request, staff!, priority, dueDate, note, now);
            request.ReviewedBy = CurrentUserId;
            request.ReviewedAt = now;
            request.Status = RepairStatus.Assigned;

            AddLog(request, RepairAction.Approved, RepairStatus.Pending, RepairStatus.Assigned, AssignmentLogNote(staff!, dueDate, note), now);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã phê duyệt và phân công REQ-{id:D4} cho {staff!.FullName}. Nhân viên bảo trì cần vào xác nhận nhận việc.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Manager: từ chối, bắt buộc ghi lý do, trả về Staff
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null) return MissingRequest();

            if (RepairStatus.Normalize(request.Status) != RepairStatus.Pending)
                return Fail(id, $"Yêu cầu REQ-{id:D4} không còn ở trạng thái chờ duyệt.");

            var reasonError = InputValidator.Text(reason, "Lý do từ chối", 500, required: true);
            if (reasonError != null) return Fail(id, reasonError);

            var now = DateTime.Now;
            var cleanReason = reason!.Trim();

            request.Status = RepairStatus.Rejected;
            request.RejectionReason = cleanReason;
            request.ReviewedBy = CurrentUserId;
            request.ReviewedAt = now;
            request.AssignedTo = null;

            AddLog(request, RepairAction.Rejected, RepairStatus.Pending, RepairStatus.Rejected, cleanReason, now);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối REQ-{id:D4} và trả về cho {await RequesterNameAsync(request)}. Nhân viên có thể chỉnh sửa và gửi lại.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Manager: phân công lại khi Maintenance Staff chưa xác nhận
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostReassignAsync(int id, int assignedStaffId, string? priority, DateTime? dueDate, string? note)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null) return MissingRequest();

            if (RepairStatus.Normalize(request.Status) != RepairStatus.Assigned)
                return Fail(id, "Chỉ phân công lại được khi nhân viên bảo trì chưa xác nhận nhận việc.");

            if (request.AssignedTo == assignedStaffId)
                return Fail(id, "Yêu cầu đã được phân công cho nhân viên này. Hãy chọn nhân viên khác.");

            var (staff, error) = await ValidateAssignmentAsync(assignedStaffId, priority, dueDate, note);
            if (error != null) return Fail(id, error);

            var now = DateTime.Now;
            ApplyAssignment(request, staff!, priority, dueDate, note, now);

            AddLog(request, RepairAction.Reassigned, RepairStatus.Assigned, RepairStatus.Assigned, AssignmentLogNote(staff!, dueDate, note), now);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã phân công lại REQ-{id:D4} cho {staff!.FullName}.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Maintenance Staff: xác nhận nhận việc
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostConfirmAsync(int id, string? note)
        {
            var check = RequireRole(RoleConstants.MaintenanceStaff);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null || request.AssignedTo != CurrentUserId) return MissingRequest();

            if (RepairStatus.Normalize(request.Status) != RepairStatus.Assigned)
                return Fail(id, "Yêu cầu này không ở trạng thái chờ xác nhận.");

            var noteError = InputValidator.Text(note, "Ghi chú", 500);
            if (noteError != null) return Fail(id, noteError);

            var now = DateTime.Now;
            request.Status = RepairStatus.InProgress;
            request.ConfirmedAt = now;
            request.BrokenReport.Status = StatusConstants.UnderMaintenance;

            AddLog(request, RepairAction.Confirmed, RepairStatus.Assigned, RepairStatus.InProgress, InputValidator.Clean(note), now);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Bạn đã xác nhận nhận việc REQ-{id:D4}. Yêu cầu chuyển sang trạng thái đang sửa chữa.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Maintenance Staff: thiếu vật tư -> tạo yêu cầu mua gửi Manager, chuyển sang Chờ vật tư
        // (nhân viên tự kiểm tra vật tư; hệ thống không quản lý tồn kho)
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostRequestPurchaseAsync(int id, string?[]? itemNames, int?[]? quantities, string?[]? units, string? note)
        {
            var check = RequireRole(RoleConstants.MaintenanceStaff);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null || request.AssignedTo != CurrentUserId) return MissingRequest();

            if (RepairStatus.Normalize(request.Status) != RepairStatus.InProgress)
                return Fail(id, "Chỉ tạo yêu cầu mua vật tư được khi yêu cầu đang ở trạng thái sửa chữa.");

            var noteError = InputValidator.Text(note, "Ghi chú mua vật tư", 300);
            if (noteError != null) return Fail(id, noteError);

            var (lines, error) = ParseItems(itemNames, quantities, units);
            if (error != null) return Fail(id, error);

            var now = DateTime.Now;
            var cleanNote = InputValidator.Clean(note);
            foreach (var l in lines)
            {
                _context.MaterialPurchaseRequests.Add(new MaterialPurchaseRequest
                {
                    MaintenanceRequestId = id,
                    ItemName = l.Name,
                    Unit = l.Unit,
                    Quantity = l.Qty,
                    Note = cleanNote,
                    RequestedBy = CurrentUserId,
                    RequestDate = now,
                    Status = PurchaseRequestStatus.Pending
                });
            }

            var summary = string.Join("; ", lines.Select(l => $"{l.Name} ×{l.Qty}{(l.Unit == null ? "" : " " + l.Unit)}"));
            request.Status = RepairStatus.WaitingMaterial;
            AddLog(request, RepairAction.PurchaseRequested, RepairStatus.InProgress, RepairStatus.WaitingMaterial,
                Clip("Thiếu vật tư: " + summary + (cleanNote != null ? ". Ghi chú: " + cleanNote : "")), now);

            var managerIds = await _context.Users
                .Where(u => u.Status == StatusConstants.Active && u.Role != null && u.Role.RoleName == RoleConstants.Manager)
                .Select(u => u.UserId)
                .ToListAsync();
            foreach (var managerId in managerIds)
            {
                Notifier.Add(_context, managerId, "Yêu cầu mua vật tư sửa chữa mới",
                    $"{CurrentFullName} cần mua cho REQ-{id:D4}: {summary}.", "/MaterialPurchases/Index?StatusFilter=Pending");
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã gửi yêu cầu mua vật tư cho Quản lý. REQ-{id:D4} chuyển sang trạng thái chờ vật tư; bạn sẽ được thông báo khi vật tư về.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Maintenance Staff: hoàn thành sửa chữa
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostCompleteAsync(int id, string? note)
        {
            var check = RequireRole(RoleConstants.MaintenanceStaff);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null || request.AssignedTo != CurrentUserId) return MissingRequest();

            if (RepairStatus.Normalize(request.Status) != RepairStatus.InProgress)
                return Fail(id, "Chỉ hoàn thành được yêu cầu đang sửa chữa.");

            var noteError = InputValidator.Text(note, "Ghi chú hoàn thành", 500, required: true);
            if (noteError != null) return Fail(id, noteError);

            var now = DateTime.Now;
            var cleanNote = note!.Trim();

            request.Status = RepairStatus.Completed;
            request.CompletedAt = now;
            request.CompletionNote = cleanNote;
            request.BrokenReport.Status = StatusConstants.Fixed;

            AddLog(request, RepairAction.Completed, RepairStatus.InProgress, RepairStatus.Completed, cleanNote, now);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã hoàn thành sửa chữa REQ-{id:D4}.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Staff: hủy yêu cầu của mình (khi chờ duyệt hoặc bị từ chối)
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostCancelAsync(int id, string? reason)
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;

            var request = await FindTrackedAsync(id);
            if (request == null || request.RequestedBy != CurrentUserId) return MissingRequest();

            var current = RepairStatus.Normalize(request.Status);
            if (current != RepairStatus.Pending && current != RepairStatus.Rejected)
                return Fail(id, "Chỉ hủy được yêu cầu đang chờ duyệt hoặc đã bị từ chối.");

            var reasonError = InputValidator.Text(reason, "Lý do hủy", 500);
            if (reasonError != null) return Fail(id, reasonError);

            request.Status = RepairStatus.Cancelled;

            AddLog(request, RepairAction.Cancelled, current, RepairStatus.Cancelled, InputValidator.Clean(reason), DateTime.Now);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã hủy yêu cầu REQ-{id:D4}.";
            return Back(id);
        }

        // ------------------------------------------------------------------
        // Vật tư: helpers
        // ------------------------------------------------------------------
        private async Task LoadPurchaseRequestsAsync(int id)
        {
            PurchaseRequests = await _context.MaterialPurchaseRequests
                .AsNoTracking()
                .Include(p => p.RequestedByNavigation)
                .Include(p => p.ApprovedByNavigation)
                .Where(p => p.MaintenanceRequestId == id)
                .OrderByDescending(p => p.RequestDate).ThenByDescending(p => p.RequestId)
                .ToListAsync();
        }

        private static (List<(string Name, int Qty, string? Unit)> Lines, string? Error) ParseItems(string?[]? names, int?[]? quantities, string?[]? units)
        {
            var lines = new List<(string Name, int Qty, string? Unit)>();
            if (names == null || quantities == null || names.Length != quantities.Length)
                return (lines, "Danh sách vật tư không hợp lệ.");

            for (var i = 0; i < names.Length; i++)
            {
                var name = InputValidator.Clean(names[i]);
                if (name == null && quantities[i] == null) continue; // dòng trống
                if (name == null) return (lines, "Vui lòng nhập tên vật tư cho mọi dòng có số lượng.");
                if (name.Length > 100) return (lines, "Tên vật tư không được vượt quá 100 ký tự.");
                if (quantities[i] is not (>= 1 and <= MaterialQtyMax))
                    return (lines, $"Số lượng của \"{name}\" phải từ 1 đến {MaterialQtyMax}.");
                var unit = units != null && i < units.Length ? InputValidator.Clean(units[i]) : null;
                if (unit != null && unit.Length > 20) return (lines, "Đơn vị tính không được vượt quá 20 ký tự.");
                lines.Add((name, quantities[i]!.Value, unit));
            }

            if (lines.Count == 0) return (lines, "Hãy nhập ít nhất một vật tư cần mua.");
            if (lines.Count > MaterialLinesMax) return (lines, $"Mỗi yêu cầu tối đa {MaterialLinesMax} vật tư.");
            return (lines, null);
        }

        private static string Clip(string text) => text.Length <= 500 ? text : text.Substring(0, 499) + "…";

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private bool CanView(MaintenanceRequest request)
        {
            if (IsStaff) return request.RequestedBy == CurrentUserId;
            if (IsMaintenanceStaff) return request.AssignedTo == CurrentUserId;
            return true; // Manager / Owner
        }

        private Task<MaintenanceRequest?> FindTrackedAsync(int id) =>
            _context.MaintenanceRequests
                .Include(r => r.BrokenReport)
                .FirstOrDefaultAsync(r => r.RequestId == id);

        private IActionResult Back(int id) => RedirectToPage(new { id });

        private IActionResult Fail(int id, string message)
        {
            ErrorMessage = message;
            return Back(id);
        }

        private IActionResult MissingRequest()
        {
            ErrorMessage = "Không tìm thấy yêu cầu sửa chữa hoặc bạn không có quyền thao tác.";
            return RedirectToPage("/Maintenance/Index");
        }

        private async Task<string> RequesterNameAsync(MaintenanceRequest request) =>
            await _context.Users.Where(u => u.UserId == request.RequestedBy).Select(u => u.FullName).FirstOrDefaultAsync() ?? "nhân viên";

        private async Task<(User? Staff, string? Error)> ValidateAssignmentAsync(int assignedStaffId, string? priority, DateTime? dueDate, string? note)
        {
            var error = InputValidator.First(
                InputValidator.Status(priority, RepairStatus.PriorityLevels, "Mức độ ưu tiên"),
                InputValidator.Text(note, "Ghi chú phân công", AssignNoteMax));
            if (error != null) return (null, error);

            if (dueDate.HasValue && dueDate.Value.Date < DateTime.Today)
                return (null, "Hạn hoàn thành không được trước ngày hôm nay.");

            var staff = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == assignedStaffId);

            if (staff == null || staff.Status != StatusConstants.Active || staff.Role?.RoleName != RoleConstants.MaintenanceStaff)
                return (null, "Vui lòng chọn một nhân viên bảo trì (Maintenance Staff) đang hoạt động.");

            return (staff, null);
        }

        private static void ApplyAssignment(MaintenanceRequest request, User staff, string? priority, DateTime? dueDate, string? note, DateTime now)
        {
            request.AssignedTo = staff.UserId;
            request.AssignedAt = now;
            request.DueDate = dueDate?.Date;
            request.AssignmentNote = InputValidator.Clean(note);
            if (!string.IsNullOrWhiteSpace(priority))
                request.Priority = priority;
        }

        private static string AssignmentLogNote(User staff, DateTime? dueDate, string? note)
        {
            var text = $"Giao cho {staff.FullName}";
            if (dueDate.HasValue) text += $", hạn {dueDate.Value:dd/MM/yyyy}";
            var clean = InputValidator.Clean(note);
            if (clean != null) text += $". Ghi chú: {clean}";
            return text;
        }

        private void AddLog(MaintenanceRequest request, string action, string? from, string? to, string? note, DateTime at)
        {
            request.Logs.Add(new MaintenanceRequestLog
            {
                Action = action,
                FromStatus = from,
                ToStatus = to,
                ActorId = CurrentUserId,
                Note = note,
                CreatedAt = at
            });
        }
    }
}
