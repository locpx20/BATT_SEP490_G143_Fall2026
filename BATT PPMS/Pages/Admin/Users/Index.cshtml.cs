using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

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
            string Username, string FullName, string Phone, string? RoleName, int? RoleId = null)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Phone) || (!RoleId.HasValue && string.IsNullOrWhiteSpace(RoleName)))
            {
                ErrorMessage = "Vui lòng điền đầy đủ tên tài khoản, số điện thoại, họ tên và chọn vai trò.";
                return RedirectToList();
            }

            var inputError = InputValidator.First(
                InputValidator.Username(Username),
                InputValidator.Text(FullName, "Họ tên", 100, required: true),
                PhoneHelper.Validate(Phone));
            if (inputError != null)
            {
                ErrorMessage = inputError;
                return RedirectToList();
            }

            var cleanUsername = Username.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername))
            {
                ErrorMessage = $"Tên tài khoản '{cleanUsername}' đã tồn tại trong hệ thống.";
                return RedirectToList();
            }

            // Số điện thoại là định danh đăng nhập nên phải duy nhất
            var cleanPhone = PhoneHelper.Normalize(Phone);
            if (await _context.Users.AnyAsync(u => u.Phone == cleanPhone))
            {
                ErrorMessage = $"Số điện thoại '{cleanPhone}' đã được dùng cho một tài khoản khác.";
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
                Password = PasswordHasher.Hash(InitialPasswordPolicy.DefaultPassword), // Mật khẩu mặc định (lưu MD5)
                Phone = cleanPhone,
                RoleId = role.RoleId,
                Status = StatusConstants.Active
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Tạo tài khoản '{newUser.Username}' thành công cho nhân viên {newUser.FullName}. Đăng nhập bằng số điện thoại {newUser.Phone}, mật khẩu mặc định: {InitialPasswordPolicy.DefaultPassword}";
            return RedirectToList();
        }

        // ---- POST: Update User (Full: Info, Role, Password) ----
        public async Task<IActionResult> OnPostUpdateUserAsync(
            int UserId,
            string FullName,
            int? RoleId,
            string? RoleName,
            string Phone,
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
                PhoneHelper.Validate(Phone));
            if (inputError != null)
            {
                ErrorMessage = inputError;
                return RedirectToList();
            }

            var cleanPhone = PhoneHelper.Normalize(Phone);
            if (await _context.Users.AnyAsync(u => u.Phone == cleanPhone && u.UserId != UserId))
            {
                ErrorMessage = $"Số điện thoại '{cleanPhone}' đã được dùng cho một tài khoản khác.";
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
            user.Phone = cleanPhone;

            bool passwordChanged = false;
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                user.Password = PasswordHasher.Hash(NewPassword.Trim());
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

        // ---- POST: Delete User ----
        public async Task<IActionResult> OnPostDeleteUserAsync(int userId)
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy tài khoản cần xóa.";
                return RedirectToList();
            }

            if (user.Role.RoleName.Equals(RoleConstants.Admin, StringComparison.OrdinalIgnoreCase) ||
                user.Role.RoleName.Equals(RoleConstants.AdminShort, StringComparison.OrdinalIgnoreCase) ||
                user.UserId == CurrentUserId)
            {
                ErrorMessage = "Không được xóa tài khoản có vai trò Admin.";
                return RedirectToList();
            }

            try
            {
                // Clear only references to the deleted user, never business records.
                // Read actual FK metadata so all tables (including price history) are covered.
                await _context.Database.ExecuteSqlRawAsync(@"
                    DECLARE @unlinkSql nvarchar(max) = N'';
                    SELECT @unlinkSql += N'UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                        + N' SET ' + QUOTENAME(c.name) + N' = NULL WHERE ' + QUOTENAME(c.name) + N' = @deletedUserId;'
                    FROM sys.foreign_key_columns fk
                    JOIN sys.tables t ON t.object_id = fk.parent_object_id
                    JOIN sys.schemas s ON s.schema_id = t.schema_id
                    JOIN sys.columns c ON c.object_id = fk.parent_object_id AND c.column_id = fk.parent_column_id
                    WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.User');
                    EXEC sys.sp_executesql @unlinkSql, N'@deletedUserId int', @deletedUserId = {0};", userId);
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (SqlException ex) when (ex.Number == 515)
            {
                ErrorMessage = "Database chưa cập nhật chức năng xóa tài khoản. Vui lòng chạy Allow_User_Deletion_Keep_BusinessData.sql.";
                return RedirectToList();
            }
            catch (DbUpdateConcurrencyException)
            {
                ErrorMessage = "Tài khoản đã được thay đổi hoặc xóa bởi người khác. Vui lòng tải lại danh sách.";
                return RedirectToList();
            }

            SuccessMessage = $"Đã xóa tài khoản '{user.Username}'. Dữ liệu nghiệp vụ được giữ nguyên.";
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

            user.Password = PasswordHasher.Hash(NewPassword.Trim());
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
