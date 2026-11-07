using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Maintenance
{
    /// <summary>
    /// Staff tạo yêu cầu sửa chữa, hoặc chỉnh sửa và gửi lại yêu cầu đã bị Manager từ chối (?id=).
    /// </summary>
    public class CreateModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        // Có giá trị khi chỉnh sửa yêu cầu bị từ chối
        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        [BindProperty] public int MachineId { get; set; }
        [BindProperty] public string Title { get; set; } = string.Empty;
        [BindProperty] public string Location { get; set; } = string.Empty;
        [BindProperty] public string? Description { get; set; }
        [BindProperty] public string Severity { get; set; } = "Medium";
        [BindProperty] public bool MachineStopped { get; set; }

        public bool IsEdit => Id.HasValue;
        public string? RejectionReason { get; private set; }
        public List<Machine> Machines { get; private set; } = new();

        // Hiển thị tại chỗ (không dùng TempData) để giữ nguyên dữ liệu đã nhập khi có lỗi
        public string? FormError { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;

            if (Id.HasValue)
            {
                var request = await _context.MaintenanceRequests
                    .AsNoTracking()
                    .Include(r => r.BrokenReport)
                    .FirstOrDefaultAsync(r => r.RequestId == Id.Value);

                var denied = CheckEditable(request);
                if (denied != null) return denied;

                MachineId = request!.BrokenReport.MachineId;
                Title = IndexModel.DisplayTitle(request.Title, request.Description);
                Location = request.Location ?? string.Empty;
                Description = NoteTags.Plain(request.Description);
                Severity = request.Priority ?? "Medium";
                MachineStopped = request.MachineStopped;
                RejectionReason = request.RejectionReason ?? NoteTags.Get(request.Description, NoteTags.Rejected);
            }

            await LoadMachinesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireRole(RoleConstants.Staff);
            if (check != null) return check;

            MaintenanceRequest? request = null;
            if (Id.HasValue)
            {
                request = await _context.MaintenanceRequests
                    .Include(r => r.BrokenReport)
                    .FirstOrDefaultAsync(r => r.RequestId == Id.Value);

                var denied = CheckEditable(request);
                if (denied != null) return denied;
                RejectionReason = request!.RejectionReason ?? NoteTags.Get(request.Description, NoteTags.Rejected);
            }

            await LoadMachinesAsync();

            var error = InputValidator.First(
                InputValidator.Text(Title, "Tiêu đề / hiện tượng", 150, required: true),
                InputValidator.Text(Location, "Vị trí máy", 200, required: true),
                InputValidator.Text(Description, "Mô tả chi tiết", 2000),
                InputValidator.Status(Severity, RepairStatus.PriorityLevels, "Mức độ nghiêm trọng"));
            if (error == null && MachineId <= 0)
                error = "Vui lòng chọn máy cần sửa chữa.";

            var machine = error == null ? Machines.FirstOrDefault(m => m.MachineId == MachineId) : null;
            if (error == null && machine == null)
                error = "Không tìm thấy máy được chọn.";

            if (error != null)
            {
                FormError = error;
                return Page();
            }

            var now = DateTime.Now;
            var title = Title.Trim();
            var location = Location.Trim();
            var detail = InputValidator.Clean(Description);
            var reportText = detail == null ? title : $"{title} — {detail}";

            if (request == null)
            {
                // Tạo mới: báo cáo sự cố + yêu cầu + nhật ký trong cùng một lần lưu
                var report = new BrokenEquipmentReport
                {
                    MachineId = machine!.MachineId,
                    Description = reportText,
                    ReportedBy = CurrentUserId,
                    ReportDate = now,
                    Status = StatusConstants.Reported,
                    Severity = Severity
                };

                var created = new MaintenanceRequest
                {
                    BrokenReport = report,
                    RequestedBy = CurrentUserId,
                    RequestDate = now,
                    Status = RepairStatus.Pending,
                    Priority = Severity,
                    Title = title,
                    Location = location,
                    MachineStopped = MachineStopped,
                    Description = detail
                };
                created.Logs.Add(new MaintenanceRequestLog
                {
                    Action = RepairAction.Created,
                    ToStatus = RepairStatus.Pending,
                    ActorId = CurrentUserId,
                    CreatedAt = now
                });

                _context.MaintenanceRequests.Add(created);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã gửi yêu cầu sửa chữa REQ-{created.RequestId:D4} cho '{machine.MachineCode} – {machine.MachineName}'. Đang chờ Quản lý duyệt.";
                return RedirectToPage("/Maintenance/Details", new { id = created.RequestId });
            }

            // Chỉnh sửa và gửi lại yêu cầu bị từ chối
            request.BrokenReport.MachineId = machine!.MachineId;
            request.BrokenReport.Description = reportText;
            request.BrokenReport.Severity = Severity;
            request.BrokenReport.Status = StatusConstants.Reported;

            request.Title = title;
            request.Location = location;
            request.MachineStopped = MachineStopped;
            request.Description = detail;
            request.Priority = Severity;
            request.Status = RepairStatus.Pending;

            request.Logs.Add(new MaintenanceRequestLog
            {
                Action = RepairAction.Resubmitted,
                FromStatus = RepairStatus.Rejected,
                ToStatus = RepairStatus.Pending,
                ActorId = CurrentUserId,
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã chỉnh sửa và gửi lại yêu cầu REQ-{request.RequestId:D4}. Đang chờ Quản lý duyệt.";
            return RedirectToPage("/Maintenance/Details", new { id = request.RequestId });
        }

        // Chỉ người tạo mới được sửa, và chỉ khi yêu cầu đang bị từ chối
        private IActionResult? CheckEditable(MaintenanceRequest? request)
        {
            if (request == null || request.RequestedBy != CurrentUserId)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu sửa chữa của bạn.";
                return RedirectToPage("/Maintenance/Index");
            }

            if (RepairStatus.Normalize(request.Status) != RepairStatus.Rejected)
            {
                TempData["ErrorMessage"] = $"Yêu cầu REQ-{request.RequestId:D4} không ở trạng thái bị từ chối nên không thể chỉnh sửa.";
                return RedirectToPage("/Maintenance/Details", new { id = request.RequestId });
            }

            return null;
        }

        private async Task LoadMachinesAsync()
        {
            Machines = await _context.Machines.AsNoTracking().OrderBy(m => m.MachineCode).ToListAsync();
        }
    }
}
