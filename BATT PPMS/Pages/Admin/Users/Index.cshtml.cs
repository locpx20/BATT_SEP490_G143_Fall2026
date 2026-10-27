using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Admin.Users
{
    /// <summary>
    /// Flat ViewModel for displaying user data in the table.
    /// </summary>
    public class UserViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string RoleName { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        // ---- Display Data ----
        public List<UserViewModel> Users { get; set; } = new();

        // ---- Filters ----
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RoleFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        // ---- Toast Messages ----
        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // ---- GET ----
        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            await LoadDataAsync();
            return Page();
        }

        // ---- POST: Create User ----
        public async Task<IActionResult> OnPostCreateUserAsync(
            string Username, string FullName, string? Email, string? Phone, string RoleName)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(RoleName))
            {
                ErrorMessage = "Vui lòng điền đầy đủ tên đăng nhập, họ tên và chọn vai trò.";
                return RedirectToPage();
            }

            var inputError = InputValidator.First(
                InputValidator.Username(Username),
                InputValidator.Text(FullName, "Họ tên", 100, required: true),
                InputValidator.Email(Email),
                InputValidator.Phone(Phone));
            if (inputError != null)
            {
                ErrorMessage = inputError;
                return RedirectToPage();
            }

            var cleanUsername = Username.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername))
            {
                ErrorMessage = $"Tên đăng nhập '{cleanUsername}' đã tồn tại trong hệ thống.";
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(Email))
            {
                var cleanEmail = Email.Trim().ToLower();
                if (await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == cleanEmail))
                {
                    ErrorMessage = $"Email '{Email}' đã được liên kết với một tài khoản khác.";
                    return RedirectToPage();
                }
            }

            // Look up role by name
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleName);
            if (role == null)
            {
                ErrorMessage = $"Vai trò '{RoleName}' không tồn tại trong hệ thống.";
                return RedirectToPage();
            }

            var newUser = new User
            {
                Username = cleanUsername,
                FullName = FullName.Trim(),
                Password = "123456",           // Default password as per policy
                Email = InputValidator.Clean(Email),
                Phone = InputValidator.Clean(Phone),
                RoleId = role.RoleId,
                Status = StatusConstants.Active
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Tạo tài khoản '{newUser.Username}' thành công cho nhân viên {newUser.FullName}. Mật khẩu mặc định: 123456";
            return RedirectToPage();
        }

        // ---- POST: Toggle Active/Locked ----
        public async Task<IActionResult> OnPostToggleStatusAsync(int userId)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToPage();
            }

            if (user.UserId == CurrentUserId)
            {
                ErrorMessage = "Bạn không thể tự khóa tài khoản Admin đang đăng nhập của chính mình.";
                return RedirectToPage();
            }

            // Protect Owner accounts
            if (user.Role?.RoleName == "Owner")
            {
                ErrorMessage = "Không thể thay đổi trạng thái tài khoản Owner.";
                return RedirectToPage();
            }

            user.Status = (user.Status == StatusConstants.Active) ? "Locked" : StatusConstants.Active;
            await _context.SaveChangesAsync();

            string newStatusText = user.Status == StatusConstants.Active ? "Đang hoạt động" : "Đã khóa";
            SuccessMessage = $"Đã cập nhật trạng thái tài khoản '{user.Username}' thành: {newStatusText}.";
            return RedirectToPage();
        }

        // ---- POST: Reset Password ----
        public async Task<IActionResult> OnPostResetPasswordAsync(int UserId, string NewPassword)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            var pwError = InputValidator.Password(NewPassword, "Mật khẩu mới");
            if (pwError != null)
            {
                ErrorMessage = pwError;
                return RedirectToPage();
            }

            var user = await _context.Users.FindAsync(UserId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToPage();
            }

            user.Password = NewPassword.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã đặt lại mật khẩu cho tài khoản '{user.Username}' thành công.";
            return RedirectToPage();
        }

        // ---- Load Data ----
        private async Task LoadDataAsync()
        {
            var query = _context.Users.Include(u => u.Role).AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(kw) ||
                    u.FullName.ToLower().Contains(kw) ||
                    (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                    (u.Phone != null && u.Phone.Contains(kw)));
            }

            // Role filter
            if (!string.IsNullOrWhiteSpace(RoleFilter))
            {
                query = query.Where(u => u.Role != null && u.Role.RoleName == RoleFilter);
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                if (StatusFilter == "active")
                    query = query.Where(u => u.Status == StatusConstants.Active);
                else if (StatusFilter == "inactive")
                    query = query.Where(u => u.Status != StatusConstants.Active);
            }

            var rawUsers = await query.OrderBy(u => u.RoleId).ThenBy(u => u.FullName).ToListAsync();

            Users = rawUsers.Select(u => new UserViewModel
            {
                UserId      = u.UserId,
                Username    = u.Username,
                FullName    = u.FullName,
                Email       = u.Email ?? "",
                PhoneNumber = u.Phone ?? "",
                RoleName    = u.Role?.RoleName ?? "Unknown",
                IsActive    = u.Status == StatusConstants.Active,
                CreatedAt   = DateTime.Today.AddDays(-u.UserId % 365)
            }).ToList();
        }
    }
}
