using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Machines
{
    public class MachineCardViewModel
    {
        public int MachineId { get; set; }
        public string MachineCode { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string Status { get; set; } = "Active";
        // Thống kê liên quan
        public int ActiveIncidents { get; set; }      // Sự cố đang chờ / đang xử lý
        public int TotalIncidents { get; set; }       // Tổng sự cố
        public DateTime? LastIncidentDate { get; set; }
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
        public string? ProcessFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public List<MachineCardViewModel> Machines { get; set; } = new();
        public List<string> ProcessNames { get; set; } = new();

        // KPI
        public int TotalMachines { get; set; }
        public int NormalCount { get; set; }
        public int WarningCount { get; set; }
        public int DownCount { get; set; }

        public bool CanManage => IsOwner || IsManager;
        public bool CanChangeStatus => IsOwner || IsManager || IsMaintenanceStaff;
        public bool CanDelete => IsOwner;

        // Bind thêm/sửa
        [BindProperty] public int EditMachineId { get; set; }
        [BindProperty] public string NewMachineCode { get; set; } = string.Empty;
        [BindProperty] public string NewMachineName { get; set; } = string.Empty;
        [BindProperty] public string NewProcessName { get; set; } = string.Empty;
        [BindProperty] public string? NewBrand { get; set; }
        [BindProperty] public string NewStatus { get; set; } = "Active";

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        private static string NormalizeMachineStatus(string? status)
        {
            return status?.Trim() switch
            {
                "Warning" => "Warning",
                "Down" => "Down",
                _ => "Active"
            };
        }

        private static string GetStatusDisplayName(string status)
        {
            return status switch
            {
                "Warning" => "Cần bảo trì",
                "Down" => "Ngừng hoạt động",
                _ => "Đang hoạt động"
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(
                RoleConstants.Owner, RoleConstants.Manager,
                RoleConstants.MaintenanceStaff, RoleConstants.Staff);
            if (check != null) return check;

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Lấy toàn bộ máy kèm báo cáo sự cố
            var machines = await _context.Machines
                .Include(m => m.BrokenEquipmentReports)
                .Include(m => m.CreatedByNavigation)
                .AsNoTracking()
                .ToListAsync();

            // Danh sách công đoạn để filter
            ProcessNames = machines
                .Select(m => m.ProcessName)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // Mapping sang ViewModel (lấy trực tiếp Status từ bảng Machine để có thể thay đổi linh hoạt)
            var vms = machines.Select(m =>
            {
                var activeIncidents = m.BrokenEquipmentReports
                    .Count(r => r.Status == "Reported" || r.Status == "Under Maintenance");
                var totalIncidents = m.BrokenEquipmentReports.Count;
                var lastIncident = m.BrokenEquipmentReports
                    .OrderByDescending(r => r.ReportDate)
                    .FirstOrDefault()?.ReportDate;

                string status = NormalizeMachineStatus(m.Status);

                return new MachineCardViewModel
                {
                    MachineId = m.MachineId,
                    MachineCode = m.MachineCode,
                    MachineName = m.MachineName,
                    ProcessName = m.ProcessName,
                    Brand = m.Brand,
                    Status = status,
                    ActiveIncidents = activeIncidents,
                    TotalIncidents = totalIncidents,
                    LastIncidentDate = lastIncident
                };
            }).ToList();

            // KPI tổng quan trước hoặc sau lọc
            TotalMachines = vms.Count;
            NormalCount = vms.Count(m => m.Status == "Active");
            WarningCount = vms.Count(m => m.Status == "Warning");
            DownCount = vms.Count(m => m.Status == "Down");

            // Filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                vms = vms.Where(m =>
                    m.MachineCode.ToLowerInvariant().Contains(term) ||
                    m.MachineName.ToLowerInvariant().Contains(term) ||
                    m.ProcessName.ToLowerInvariant().Contains(term) ||
                    (m.Brand != null && m.Brand.ToLowerInvariant().Contains(term))
                ).ToList();
            }

            if (!string.IsNullOrWhiteSpace(ProcessFilter) && ProcessFilter != "ALL")
            {
                vms = vms.Where(m => m.ProcessName == ProcessFilter).ToList();
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                vms = vms.Where(m => m.Status == StatusFilter).ToList();
            }

            Machines = vms.OrderBy(m => m.MachineCode).ToList();
        }

        // ── Thêm máy mới ──
        public async Task<IActionResult> OnPostAddMachineAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(NewMachineCode) ||
                string.IsNullOrWhiteSpace(NewMachineName) ||
                string.IsNullOrWhiteSpace(NewProcessName))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ Mã máy, Tên máy và Công đoạn.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            var exists = await _context.Machines
                .AnyAsync(m => m.MachineCode == NewMachineCode.Trim());
            if (exists)
            {
                ErrorMessage = $"Mã máy '{NewMachineCode.Trim()}' đã tồn tại trong hệ thống.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            var machine = new Machine
            {
                MachineCode = NewMachineCode.Trim().ToUpper(),
                MachineName = NewMachineName.Trim(),
                ProcessName = NewProcessName.Trim(),
                Brand = string.IsNullOrWhiteSpace(NewBrand) ? null : NewBrand.Trim(),
                Status = NormalizeMachineStatus(NewStatus),
                CreatedBy = CurrentUserId
            };

            _context.Machines.Add(machine);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã thêm thiết bị '{machine.MachineName}' ({machine.MachineCode}) vào danh mục.";
            return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
        }

        // ── Sửa thông tin máy ──
        public async Task<IActionResult> OnPostEditMachineAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            var machine = await _context.Machines.FindAsync(EditMachineId);
            if (machine == null)
            {
                ErrorMessage = "Không tìm thấy thiết bị cần cập nhật.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            if (string.IsNullOrWhiteSpace(NewMachineName) ||
                string.IsNullOrWhiteSpace(NewProcessName))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ Tên máy và Công đoạn.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            // Kiểm tra trùng mã (nếu đổi mã)
            if (!string.IsNullOrWhiteSpace(NewMachineCode) &&
                NewMachineCode.Trim().ToUpper() != machine.MachineCode)
            {
                var codeExists = await _context.Machines
                    .AnyAsync(m => m.MachineCode == NewMachineCode.Trim().ToUpper()
                                   && m.MachineId != EditMachineId);
                if (codeExists)
                {
                    ErrorMessage = $"Mã máy '{NewMachineCode.Trim()}' đã tồn tại.";
                    return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
                }
                machine.MachineCode = NewMachineCode.Trim().ToUpper();
            }

            machine.MachineName = NewMachineName.Trim();
            machine.ProcessName = NewProcessName.Trim();
            machine.Brand = string.IsNullOrWhiteSpace(NewBrand) ? null : NewBrand.Trim();
            machine.Status = NormalizeMachineStatus(NewStatus);

            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã cập nhật thông tin thiết bị '{machine.MachineName}'.";
            return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
        }

        // ── Đổi nhanh trạng thái máy ──
        public async Task<IActionResult> OnPostChangeStatusAsync(int machineId, string status)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff);
            if (check != null) return check;

            var machine = await _context.Machines.FindAsync(machineId);
            if (machine == null)
            {
                ErrorMessage = "Không tìm thấy thiết bị cần đổi trạng thái.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            var normalized = NormalizeMachineStatus(status);
            machine.Status = normalized;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã chuyển trạng thái máy '{machine.MachineName}' ({machine.MachineCode}) sang '{GetStatusDisplayName(normalized)}'.";
            return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
        }

        // ── Xóa máy ──
        public async Task<IActionResult> OnPostDeleteMachineAsync(int machineId)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var machine = await _context.Machines
                .Include(m => m.BrokenEquipmentReports)
                .FirstOrDefaultAsync(m => m.MachineId == machineId);

            if (machine == null)
            {
                ErrorMessage = "Không tìm thấy thiết bị.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            if (machine.BrokenEquipmentReports.Any())
            {
                ErrorMessage = $"Không thể xóa '{machine.MachineName}' vì đã có {machine.BrokenEquipmentReports.Count} báo cáo sự cố liên quan.";
                return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
            }

            _context.Machines.Remove(machine);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã xóa thiết bị '{machine.MachineName}' khỏi danh mục.";
            return RedirectToPage(new { SearchTerm, ProcessFilter, StatusFilter });
        }
    }
}
