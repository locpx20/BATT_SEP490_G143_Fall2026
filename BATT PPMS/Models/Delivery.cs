using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

/// <summary>Yêu cầu xuất kho: thông tin tài xế đến lấy hàng cho một đơn bán đã duyệt.</summary>
public partial class Delivery
{
    public int DeliveryId { get; set; }

    public int OrderId { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string DriverName { get; set; } = null!;

    public string? DriverPhone { get; set; }

    public DateTime PickupDate { get; set; }

    public string? Note { get; set; }

    /// <summary>Lý do thủ kho trả lại để Manager chỉnh sửa.</summary>
    public string? ReturnReason { get; set; }

    public string? DeliveryStatus { get; set; }

    public int? ConfirmedBy { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? ConfirmedByNavigation { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ExportSlip? ExportSlip { get; set; }
}
