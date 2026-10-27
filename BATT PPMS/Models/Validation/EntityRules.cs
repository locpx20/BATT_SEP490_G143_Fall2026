using System.ComponentModel.DataAnnotations;

namespace BATT_PPMS.Models;

// Quy tắc nghiệp vụ liên quan nhiều trường (metadata đơn lẻ nằm ở EntityMetadata.cs).
// Chạy khi Validator.TryValidateObject (sau khi các quy tắc từng trường đã đạt).

public partial class Order : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (TotalAmount != Math.Round(Quantity * UnitPrice, 2))
            yield return new ValidationResult("Tổng tiền phải bằng số lượng × đơn giá.", new[] { nameof(TotalAmount) });
        if (Status == "Rejected" && ApprovedBy == null)
            yield return new ValidationResult("Đơn bị từ chối phải ghi nhận người xử lý.", new[] { nameof(ApprovedBy) });
    }
}

public partial class Qcinspection : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Result))
        {
            if (InspectedBy == null)
                yield return new ValidationResult("Phiếu đã có kết quả phải có người kiểm tra.", new[] { nameof(InspectedBy) });
            if (InspectDate == null)
                yield return new ValidationResult("Phiếu đã có kết quả phải có ngày kiểm tra.", new[] { nameof(InspectDate) });
        }
        if (InspectDate != null && InspectDate < RequestDate)
            yield return new ValidationResult("Ngày kiểm tra không được trước ngày yêu cầu.", new[] { nameof(InspectDate) });
    }
}

public partial class MaintenanceRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if ((Status == "Completed" || Status == "In Progress" || Status == "InProgress") && AssignedTo == null)
            yield return new ValidationResult("Yêu cầu đang/đã sửa chữa phải được phân công người phụ trách.", new[] { nameof(AssignedTo) });
    }
}

public partial class PurchaseInvoice : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if ((Status == "Approved" || Status == "Rejected") && ApprovedBy == null)
            yield return new ValidationResult("Hóa đơn đã duyệt/từ chối phải ghi nhận người duyệt.", new[] { nameof(ApprovedBy) });
    }
}

public partial class MaterialPurchaseRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if ((Status == "Approved" || Status == "Rejected") && ApprovedBy == null)
            yield return new ValidationResult("Yêu cầu đã duyệt/từ chối phải ghi nhận người duyệt.", new[] { nameof(ApprovedBy) });
    }
}
