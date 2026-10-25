using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Admin.Users
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<User> UsersList { get; set; } = new();
        public List<Role> RolesList { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateUserAsync(
            string username, string fullName, string password, string? email, string? phone, int roleId)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Vui lòng điền đầy đủ tên đăng nhập, họ tên và mật khẩu.";
                return RedirectToPage();
            }

            var cleanUsername = username.Trim();
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
            {
                ErrorMessage = $"Tên đăng nhập '{cleanUsername}' đã tồn tại trong hệ thống.";
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var cleanEmail = email.Trim().ToLower();
                if (await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == cleanEmail))
                {
                    ErrorMessage = $"Email '{email}' đã được liên kết với một tài khoản khác.";
                    return RedirectToPage();
                }
            }

            var newUser = new User
            {
                Username = cleanUsername,
                FullName = fullName.Trim(),
                Password = password.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                RoleId = roleId,
                Status = StatusConstants.Active
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Tạo tài khoản '{newUser.Username}' thành công cho nhân viên {newUser.FullName}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int userId)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            var user = await _context.Users.FindAsync(userId);
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

            user.Status = (user.Status == StatusConstants.Active) ? "Locked" : StatusConstants.Active;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã cập nhật trạng thái tài khoản '{user.Username}' thành: {(user.Status == StatusConstants.Active ? "Đang hoạt động" : "Đã khóa")}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(int userId, string newPassword)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToPage();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToPage();
            }

            user.Password = newPassword.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã đặt lại mật khẩu mới cho tài khoản '{user.Username}' thành công.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateRoleAsync(int userId, int newRoleId)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToPage();
            }

            user.RoleId = newRoleId;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã cập nhật vai trò cho người dùng '{user.Username}' thành công.";
            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            UsersList = await _context.Users
                .Include(u => u.Role)
                .OrderBy(u => u.UserId)
                .ToListAsync();

            RolesList = await _context.Roles
                .OrderBy(r => r.RoleId)
                .ToListAsync();
        }
    }
}
