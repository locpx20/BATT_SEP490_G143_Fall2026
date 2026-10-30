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
        public int RoleId { get; set; }
        public string RoleName { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
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
        public List<Role> AllRoles { get; set; } = new();
        public PageWindow Pagination { get; private set; } = new(0, 1);

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        // ---- Current Logged In Admin Info ----
        public int LoggedInUserId => CurrentUserId;
        public string LoggedInUsername => CurrentUsername;

        // ---- Filters ----
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }


        [BindProperty(Name = "search", SupportsGet = true)]
        public string? Search { get; set; }

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
            PageNumber = PageWindow.ReadRequestedPage(Request.Query, PageNumber);
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(SearchTerm) && !string.IsNullOrWhiteSpace(Search))
            {
                SearchTerm = Search;
            }

            await LoadDataAsync();
            return Page();
        }

        // ---- POST: Create User ----
        public async Task<IActionResult> OnPostCreateUserAsync(
            string Username, string FullName, string? Phone, string? RoleName, int? RoleId = null)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName) || (!RoleId.HasValue && string.IsNullOrWhiteSpace(RoleName)))
            {
                ErrorMessage = "Vui lòng điền đầy đủ tên đăng nhập, họ tên và chọn vai trò.";
                return RedirectToList();
            }

            var inputError = InputValidator.First(
                InputValidator.Username(Username),
                InputValidator.Text(FullName, "Họ tên", 100, required: true),
                InputValidator.Phone(Phone));
            if (inputError != null)
            {
                ErrorMessage = inputError;
                return RedirectToList();
            }

            var cleanUsername = Username.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername))
            {
                ErrorMessage = $"Tên đăng nhập '{cleanUsername}' đã tồn tại trong hệ thống.";
                return RedirectToList();
            }

            // Look up role
            Role? role = null;
            if (RoleId.HasValue && RoleId.Value > 0)
            {
                role = await _context.Roles.FindAsync(RoleId.Value);
            }
            if (role == null && !string.IsNullOrWhiteSpace(RoleName))
            {
                role = await _context.Roles.FirstOrDefaultAsync(r => 
                    r.RoleName == RoleName || 
                    (RoleName == "Admin" && r.RoleName == "Admin System") ||
                    (RoleName == "Admin System" && r.RoleName == "Admin"));
            }

            if (role == null)
            {
                ErrorMessage = "Vai trò được chọn không tồn tại trong hệ thống.";
                return RedirectToList();
            }

            var newUser = new User
            {
                CreatedAt = DateTime.UtcNow,
                Username = cleanUsername,
                FullName = FullName.Trim(),
                Password = "123456",           // Default password as per policy
                Phone = InputValidator.Clean(Phone),
                RoleId = role.RoleId,
                Status = StatusConstants.Active
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Tạo tài khoản '{newUser.Username}' thành công cho nhân viên {newUser.FullName}. Mật khẩu mặc định: 123456";
            return RedirectToList();
        }

        // ---- POST: Update User (Full: Info, Role, Password) ----
        public async Task<IActionResult> OnPostUpdateUserAsync(
            int UserId,
            string FullName,
            int? RoleId,
            string? RoleName,
            string? Phone,
            string? NewPassword)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = "Họ và tên không được để trống.";
                return RedirectToList();
            }

            var inputError = InputValidator.First(
                InputValidator.Text(FullName, "Họ tên", 100, required: true),
                InputValidator.Phone(Phone));
            if (inputError != null)
            {
                ErrorMessage = inputError;
                return RedirectToList();
            }

            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                var pwError = InputValidator.Password(NewPassword, "Mật khẩu mới");
                if (pwError != null)
                {
                    ErrorMessage = pwError;
                    return RedirectToList();
                }
            }

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == UserId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToList();
            }

            // Look up target role if provided
            if ((RoleId.HasValue && RoleId.Value > 0) || !string.IsNullOrWhiteSpace(RoleName))
            {
                Role? role = null;
                if (RoleId.HasValue && RoleId.Value > 0)
                {
                    role = await _context.Roles.FindAsync(RoleId.Value);
                }
                if (role == null && !string.IsNullOrWhiteSpace(RoleName))
                {
                    role = await _context.Roles.FirstOrDefaultAsync(r => 
                        r.RoleName == RoleName || 
                        (RoleName == "Admin" && r.RoleName == "Admin System") ||
                        (RoleName == "Admin System" && r.RoleName == "Admin"));
                }

                if (role == null)
                {
                    ErrorMessage = "Vai trò được chọn không hợp lệ hoặc không tồn tại.";
                    return RedirectToList();
                }

                var isSelf = (user.UserId == CurrentUserId) || 
                             (!string.IsNullOrEmpty(CurrentUsername) && user.Username.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase));
                if (isSelf && !role.RoleName.Contains("Admin"))
                {
                    ErrorMessage = "Bạn không thể đổi vai trò của tài khoản Admin đang đăng nhập sang vai trò khác để tránh mất quyền quản trị.";
                    return RedirectToList();
                }

                user.RoleId = role.RoleId;
            }

            user.FullName = FullName.Trim();
            user.Phone = InputValidator.Clean(Phone);

            bool passwordChanged = false;
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                user.Password = NewPassword.Trim();
                passwordChanged = true;
            }

            await _context.SaveChangesAsync();

            SuccessMessage = passwordChanged
                ? $"Đã cập nhật thông tin và mật khẩu mới cho tài khoản '{user.Username}' thành công."
                : $"Đã cập nhật thông tin cho tài khoản '{user.Username}' thành công.";
            return RedirectToList();
        }

        // ---- POST: Update User Role ----
        public async Task<IActionResult> OnPostUpdateRoleAsync(int UserId, int? RoleId, string? RoleName)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == UserId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToList();
            }

            // Look up target role
            Role? role = null;
            if (RoleId.HasValue && RoleId.Value > 0)
            {
                role = await _context.Roles.FindAsync(RoleId.Value);
            }
            if (role == null && !string.IsNullOrWhiteSpace(RoleName))
            {
                role = await _context.Roles.FirstOrDefaultAsync(r => 
                    r.RoleName == RoleName || 
                    (RoleName == "Admin" && r.RoleName == "Admin System") ||
                    (RoleName == "Admin System" && r.RoleName == "Admin"));
            }

            if (role == null)
            {
                ErrorMessage = "Vai trò được chọn không hợp lệ hoặc không tồn tại.";
                return RedirectToList();
            }

            // Protect logged-in admin from losing Admin role
            var isSelf = (user.UserId == CurrentUserId) || 
                         (!string.IsNullOrEmpty(CurrentUsername) && user.Username.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase));
            if (isSelf && !role.RoleName.Contains("Admin"))
            {
                ErrorMessage = "Bạn không thể đổi vai trò của tài khoản Admin đang đăng nhập sang vai trò khác để tránh mất quyền quản trị.";
                return RedirectToList();
            }

            var oldRoleName = user.Role?.RoleName ?? "Chưa phân quyền";
            user.RoleId = role.RoleId;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Cập nhật vai trò cho tài khoản '{user.Username}' thành công: [{oldRoleName}] ➔ [{role.RoleName}].";
            return RedirectToList();
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
                return RedirectToList();
            }

            if (user.UserId == CurrentUserId || user.Username.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Bạn không thể tự khóa tài khoản Admin đang đăng nhập của chính mình.";
                return RedirectToList();
            }

            // Owner accounts are now treated the same as other roles (no special blocking)
            user.Status = (user.Status == StatusConstants.Active) ? "Locked" : StatusConstants.Active;
            await _context.SaveChangesAsync();

            string newStatusText = user.Status == StatusConstants.Active ? "Đang hoạt động" : "Đã khóa";
            SuccessMessage = $"Đã cập nhật trạng thái tài khoản '{user.Username}' thành: {newStatusText}.";
            return RedirectToList();
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
                return RedirectToList();
            }

            var user = await _context.Users.FindAsync(UserId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return RedirectToList();
            }

            user.Password = NewPassword.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã đặt lại mật khẩu cho tài khoản '{user.Username}' thành công.";
            return RedirectToList();
        }

        private IActionResult RedirectToList()
        {
            var url = Url.Page("/Admin/Users/Index", new { SearchTerm, RoleFilter, StatusFilter })!;
            return LocalRedirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                url, "pageNumber", PageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        // ---- Load Data ----
        private async Task LoadDataAsync()
        {
            AllRoles = await _context.Roles.OrderBy(r => r.RoleId).ToListAsync();

            var query = _context.Users.Include(u => u.Role).AsQueryable();

            SearchTerm = InputValidator.Clean(SearchTerm);
            query = UserSearch.Apply(query, SearchTerm, "all", RoleFilter, StatusFilter);

            Pagination = new PageWindow(await query.CountAsync(), PageNumber);
            var rawUsers = await query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.UserId)
                .Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();

            Users = rawUsers.Select(u => new UserViewModel
            {
                UserId      = u.UserId,
                Username    = u.Username,
                FullName    = u.FullName,
                Email       = u.Email ?? "",
                PhoneNumber = u.Phone ?? "",
                RoleId      = u.RoleId,
                RoleName    = u.Role?.RoleName ?? "Unknown",
                IsActive    = u.Status == StatusConstants.Active,
                CreatedAt   = u.CreatedAt
            }).ToList();
        }
    }
}
