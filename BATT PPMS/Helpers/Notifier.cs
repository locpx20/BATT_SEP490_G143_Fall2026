using BATT_PPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Helpers;

/// <summary>Thông báo nội bộ: tạo mới và lấy các thông báo chưa đọc.</summary>
public static class Notifier
{
    public static void Add(ProductionManagementDbContext context, int userId, string title, string message, string? url)
    {
        context.AppNotifications.Add(new AppNotification
        {
            UserId = userId,
            Title = Trim(title, 150),
            Message = Trim(message, 500),
            Url = url,
            IsRead = false,
            CreatedAt = DateTime.Now
        });
    }

    /// <summary>Lấy thông báo chưa đọc của người dùng và đánh dấu đã đọc.</summary>
    public static async Task<List<AppNotification>> TakeUnreadAsync(ProductionManagementDbContext context, int userId)
    {
        if (userId <= 0) return new List<AppNotification>();

        var items = await context.AppNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync();

        if (items.Count > 0)
        {
            foreach (var n in items) n.IsRead = true;
            await context.SaveChangesAsync();
        }
        return items;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}
