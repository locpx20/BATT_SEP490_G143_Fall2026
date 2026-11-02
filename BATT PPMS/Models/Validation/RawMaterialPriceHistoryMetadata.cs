#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BATT_PPMS.Models;

[MetadataType(typeof(RawMaterialPriceHistoryMetadata))]
public partial class RawMaterialPriceHistory { }

public class RawMaterialPriceHistoryMetadata
{
    [Display(Name = "Giá cũ")]
    [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "{0} phải lớn hơn 0.")]
    public object? OldPrice { get; set; }

    [Display(Name = "Giá mới")]
    [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "{0} phải lớn hơn 0.")]
    public object? NewPrice { get; set; }

    [Display(Name = "Lý do thay đổi")]
    [StringLength(500, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    public object? Reason { get; set; }
}
