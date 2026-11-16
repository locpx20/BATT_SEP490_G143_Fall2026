namespace BATT_PPMS.Models;

/// <summary>Phiếu xuất kho thành phẩm do thủ kho lập sau khi kiểm tra yêu cầu xuất kho.</summary>
public class ExportSlip
{
    public int SlipId { get; set; }
    public string SlipCode { get; set; } = null!;
    public int DeliveryId { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = ExportSlipStatus.Processing;
    public string? Note { get; set; }
    /// <summary>Đường dẫn ảnh phiếu xuất kho thực tế (tùy chọn).</summary>
    public string? ImagePath { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual Delivery Delivery { get; set; } = null!;
    public virtual Order Order { get; set; } = null!;
    public virtual User? CreatedByNavigation { get; set; }
    public virtual User? CompletedByNavigation { get; set; }
    public virtual ICollection<ExportSlipLine> Lines { get; set; } = new List<ExportSlipLine>();
}

public class ExportSlipLine
{
    public int LineId { get; set; }
    public int SlipId { get; set; }
    public int BatchId { get; set; }
    public string? Grade { get; set; }
    public int Bags { get; set; }
    public decimal Quantity { get; set; }

    public virtual ExportSlip Slip { get; set; } = null!;
    public virtual FinishedGoodBatch Batch { get; set; } = null!;
}

public static class ExportSlipStatus
{
    public const string Processing = "Processing";
    public const string Completed = "Completed";
}
