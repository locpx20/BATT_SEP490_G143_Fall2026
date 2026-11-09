namespace BATT_PPMS.Helpers;

/// <summary>Explicit allowlist of role view folders; never use session text as a view path.</summary>
public static class RoleViews
{
    public static string? Folder(string? roleName) => roleName switch
    {
        RoleConstants.Admin or RoleConstants.AdminShort => "Admin",
        RoleConstants.Owner => "Owner",
        RoleConstants.Manager => "Manager",
        RoleConstants.PurchasingStaff => "Purchasing",
        RoleConstants.WarehouseStaff => "Warehouse",
        RoleConstants.Staff => "Production",
        RoleConstants.QCStaff => "QualityControl",
        RoleConstants.MaintenanceStaff => "Maintenance",
        _ => null
    };

    /// <summary>Mọi role có thư mục giao diện, trừ Admin, dùng chung khung workspace (sidebar xanh, tài khoản cuối sidebar).</summary>
    public static bool IsWorkspaceRole(string? roleName) =>
        Folder(roleName) is string folder && folder != "Admin";
}
