using System;
using System.Collections.Generic;

namespace BATT_PPMS.Models;

/// <summary>Mỗi dòng = một lần thay đổi giá thu mua của nguyên liệu (chỉ thêm, không sửa/xoá).</summary>
public partial class RawMaterialPriceHistory
{
    public int HistoryId { get; set; }

    public int MaterialId { get; set; }

    // null = lần thiết lập giá đầu tiên
    public decimal? OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public int ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Reason { get; set; }

    public virtual RawMaterial Material { get; set; } = null!;

    public virtual User ChangedByNavigation { get; set; } = null!;
}
