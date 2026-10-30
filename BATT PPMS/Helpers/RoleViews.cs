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
}
