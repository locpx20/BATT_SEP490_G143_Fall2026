using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Helpers;

public enum WarehouseNavigationNotice { Plans, FinishedStock }

public static class WarehouseNavigationNotices
{
    private static IQueryable<AppNotification> Unread(ProductionManagementDbContext db, int userId, WarehouseNavigationNotice kind)
    {
        var query = db.AppNotifications.Where(n => n.UserId == userId && !n.IsRead && n.Url != null);
        return kind == WarehouseNavigationNotice.Plans
            ? query.Where(n => n.Url!.StartsWith("/Warehouse/Index?tab=plans") || n.Url.StartsWith("/Warehouse?tab=plans"))
            : query.Where(n => n.Url!.StartsWith("/Inventory/Index?tab=finished") || n.Url.StartsWith("/Inventory?tab=finished"));
    }

    public static Task<int> CountAsync(ProductionManagementDbContext db, int userId, WarehouseNavigationNotice kind, CancellationToken cancellationToken) =>
        Unread(db, userId, kind).CountAsync(cancellationToken);

    // Other warehouse screens must not mark these two navigation categories as read.
    public static IQueryable<AppNotification> ExcludeFrom(IQueryable<AppNotification> query) =>
        query.Where(n => n.Url == null || !(n.Url.StartsWith("/Warehouse/Index?tab=plans")
            || n.Url.StartsWith("/Warehouse?tab=plans") || n.Url.StartsWith("/Inventory/Index?tab=finished")
            || n.Url.StartsWith("/Inventory?tab=finished")));

    public static async Task<List<AppNotification>> OpenAsync(ProductionManagementDbContext db, int userId, WarehouseNavigationNotice kind, CancellationToken cancellationToken)
    {
        var query = Unread(db, userId, kind);
        var items = await query.AsNoTracking().OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId)
            .Take(10).ToListAsync(cancellationToken);
        await query.ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);
        return items;
    }
}
