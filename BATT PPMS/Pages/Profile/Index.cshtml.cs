using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Profile
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }

        public User? User { get; set; }

        public string RoleDisplayName => User?.Role?.RoleName switch
        {
            RoleConstants.Admin or RoleConstants.AdminShort => "Quản trị viên hệ thống",
            RoleConstants.Owner => "Giám đốc / Chủ sở hữu",
            RoleConstants.Manager => "Quản lý vận hành",
            RoleConstants.Staff => "Nhân viên sản xuất",
            RoleConstants.WarehouseStaff => "Thủ kho",
            RoleConstants.PurchasingStaff => "Nhân viên thu mua",
            RoleConstants.QCStaff => "Nhân viên kiểm tra chất lượng",
            RoleConstants.MaintenanceStaff => "Nhân viên bảo trì",
            _ => User?.Role?.RoleName ?? "Chưa phân vai trò"
        };

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public string FullName { get; set; } = string.Empty;
        [BindProperty]
        public string? Phone { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            User = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            var error = InputValidator.First(
                InputValidator.Text(FullName, "Họ và tên", 100, required: true),
                PhoneHelper.Validate(Phone));

            if (error != null)
            {
                ErrorMessage = error;
                return RedirectToPage("/Profile/Index");
            }

            // Số điện thoại là định danh đăng nhập nên phải duy nhất
            var cleanPhone = PhoneHelper.Normalize(Phone);
            if (await _context.Users.AnyAsync(u => u.Phone == cleanPhone && u.UserId != user.UserId))
            {
                ErrorMessage = $"Số điện thoại '{cleanPhone}' đã được dùng cho một tài khoản khác.";
                return RedirectToPage("/Profile/Index");
            }

            user.FullName = FullName.Trim();
            user.Phone = cleanPhone;
            await _context.SaveChangesAsync();

            // Cập nhật lại session
            HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
            SuccessMessage = "Cập nhật hồ sơ thành công.";

            return RedirectToPage("/Profile/Index");
        }
    }
}
