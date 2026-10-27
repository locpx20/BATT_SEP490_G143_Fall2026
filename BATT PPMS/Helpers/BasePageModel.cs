using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BATT_PPMS.Helpers
{
    /// <summary>
    /// Base PageModel cho tất cả các trang yêu cầu đăng nhập.
    /// Kế thừa class này để tự động kiểm tra session và role.
    /// </summary>
    public abstract class BasePageModel : PageModel
    {
        // Thông tin user từ session
        protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        protected string CurrentUsername => HttpContext.Session.GetString(SessionKeys.Username) ?? string.Empty;
        protected string CurrentFullName => HttpContext.Session.GetString(SessionKeys.FullName) ?? string.Empty;
        protected int CurrentRoleId => HttpContext.Session.GetInt32(SessionKeys.RoleId) ?? 0;
        protected string CurrentRoleName => HttpContext.Session.GetString(SessionKeys.RoleName) ?? string.Empty;

        // Kiểm tra trạng thái đăng nhập
        protected bool IsLoggedIn => CurrentUserId > 0;

        // --- Kiểm tra từng role (khớp hoàn toàn với use case diagram và DB mới) ---
        protected bool IsAdmin => CurrentRoleName == RoleConstants.Admin || CurrentRoleName == RoleConstants.AdminShort || CurrentRoleName.Contains("Admin");
        protected bool IsOwner => CurrentRoleName == RoleConstants.Owner;
        protected bool IsManager => CurrentRoleName == RoleConstants.Manager;
        protected bool IsStaff => CurrentRoleName == RoleConstants.Staff;
        protected bool IsMaintenanceStaff => CurrentRoleName == RoleConstants.MaintenanceStaff;
        protected bool IsQCStaff => CurrentRoleName == RoleConstants.QCStaff;
        protected bool IsPurchasingStaff => CurrentRoleName == RoleConstants.PurchasingStaff;
        protected bool IsWarehouseStaff => CurrentRoleName == RoleConstants.WarehouseStaff;

        // --- Helpers kết hợp hay dùng ---
        protected bool IsOwnerOrManager => IsOwner || IsManager;

        /// <summary>
        /// Yêu cầu đăng nhập. Nếu chưa, redirect về Login.
        /// </summary>
        protected IActionResult? RequireLogin()
        {
            if (!IsLoggedIn)
                return RedirectToPage("/Auth/Login");
            return null;
        }

        /// <summary>
        /// Yêu cầu quyền theo danh sách role cho phép.
        /// </summary>
        protected IActionResult? RequireRole(params string[] allowedRoles)
        {
            var loginCheck = RequireLogin();
            if (loginCheck != null) return loginCheck;

            var match = allowedRoles.Any(r => 
                r.Equals(CurrentRoleName, StringComparison.OrdinalIgnoreCase) ||
                (r.Contains("Admin") && CurrentRoleName.Contains("Admin")));

            if (!match)
                return RedirectToPage("/Auth/AccessDenied");

            return null;
        }
    }
}
