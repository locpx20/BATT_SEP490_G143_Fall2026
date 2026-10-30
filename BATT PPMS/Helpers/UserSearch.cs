using BATT_PPMS.Models;

namespace BATT_PPMS.Helpers;

public static class UserSearch
{
    public static string NormalizeField(string? field) => field?.Trim().ToLowerInvariant() switch
    {
        "fullname" => "fullname",
        "username" => "username",
        "phone" => "phone",
        _ => "all"
    };

    public static IQueryable<User> Apply(IQueryable<User> query, string? term,
        string? field, string? role, string? status)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            var keyword = term.Trim().ToLowerInvariant();
            query = NormalizeField(field) switch
            {
                "fullname" => query.Where(u => u.FullName.ToLower().Contains(keyword)),
                "username" => query.Where(u => u.Username.ToLower().Contains(keyword)),
                "phone" => query.Where(u => u.Phone != null && u.Phone.Contains(keyword)),
                _ => query.Where(u => u.Username.ToLower().Contains(keyword) ||
                    u.FullName.ToLower().Contains(keyword) ||
                    (u.Phone != null && u.Phone.Contains(keyword)))
            };
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleName = role.Trim();
            query = roleName is RoleConstants.Admin or RoleConstants.AdminShort
                ? query.Where(u => u.Role != null &&
                    (u.Role.RoleName == RoleConstants.Admin || u.Role.RoleName == RoleConstants.AdminShort))
                : query.Where(u => u.Role != null && u.Role.RoleName == roleName);
        }

        return status?.Trim().ToLowerInvariant() switch
        {
            "active" => query.Where(u => u.Status == StatusConstants.Active),
            "inactive" => query.Where(u => u.Status != StatusConstants.Active),
            _ => query
        };
    }
}
