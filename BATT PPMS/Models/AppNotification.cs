using System;

namespace BATT_PPMS.Models;

/// <summary>Thông báo nội bộ gửi tới một người dùng.</summary>
public partial class AppNotification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? Url { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
