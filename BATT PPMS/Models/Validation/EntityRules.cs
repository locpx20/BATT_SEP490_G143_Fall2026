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
    }
}

public partial class Qcinspection : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Result))
        {
            if (InspectDate == null)
                yield return new ValidationResult("Phiếu đã có kết quả phải có ngày kiểm tra.", new[] { nameof(InspectDate) });
        }
        if (InspectDate != null && InspectDate < RequestDate)
            yield return new ValidationResult("Ngày kiểm tra không được trước ngày yêu cầu.", new[] { nameof(InspectDate) });
    }
}

// User references can be null after account deletion. Page handlers still assign
// the current user on new actions; historical records remain valid without them.
