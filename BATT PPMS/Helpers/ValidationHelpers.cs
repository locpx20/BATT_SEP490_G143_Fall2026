using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BATT_PPMS.Helpers
{
    /// <summary>Các regex dùng chung (hằng số để dùng được trong attribute).</summary>
    public static class ValidationPatterns
    {
        public const string Phone = @"^[0-9+()\-\s.]{8,20}$";
        public const string Username = @"^[A-Za-z0-9._-]{3,50}$";
        // Cột User.password là VARCHAR (không phải NVARCHAR) -> ký tự có dấu sẽ bị hỏng khi lưu.
        public const string AsciiPassword = @"^[\x21-\x7E]([\x20-\x7E]*[\x21-\x7E])?$";
    }

    /// <summary>
    /// Tập giá trị Status/Priority hợp lệ cho từng bảng. Thêm trạng thái mới ở đây.
    /// </summary>
    public static class StatusSets
    {
        public static readonly string[] UserStatuses = { "Active", "Inactive", "Locked" };
        public static readonly string[] PartnerStatuses = { "Active", "Inactive" };
        public static readonly string[] InventoryStatuses = { "Available", "Unavailable", "Inactive" };
        public static readonly string[] OrderStatuses = { "Pending", "Approved", "Rejected", "Completed", "Cancelled" };
        public static readonly string[] DeliveryStatuses = { "Prepared", "In Transit", "Delivered", "Cancelled" };
        public static readonly string[] EquipmentStatuses = { "Reported", "Under Maintenance", "Fixed" };
        public static readonly string[] PriorityLevels = { "Low", "Normal", "Medium", "High", "Critical" };
        public static readonly string[] MaintenanceStatuses = { "Pending", "Reported", "Assigned", "In Progress", "InProgress", "Completed", "Rejected" };
        public static readonly string[] AssignmentStatuses = { "Assigned", "In Progress", "Done", "Cancelled" };
        public static readonly string[] ShiftReportStatuses = { "Submitted", "Pending", "Approved", "Rejected" };
        public static readonly string[] PurchaseStatuses = { "Pending", "Submitted", "Approved", "Rejected", "Received", "Completed", "Cancelled" };
        public static readonly string[] RequestStatuses = { "Pending", "Approved", "Rejected", "Completed", "Cancelled" };
        public static readonly string[] QcStatuses = { "Pending", "In Progress", "Completed" };
        public static readonly string[] QcResults = { "Pass", "Fail" };

        public static string[] Get(string name) =>
            (string[]?)typeof(StatusSets).GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new ArgumentException($"StatusSets.{name} không tồn tại");

        public static bool IsValid(string[] set, string? value) =>
            value == null || set.Contains(value, StringComparer.Ordinal);
    }

    /// <summary>Giá trị phải thuộc một tập trạng thái (null được chấp nhận vì cột cho phép NULL/có DEFAULT).</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class AllowedStatusAttribute : ValidationAttribute
    {
        private readonly string _setName;
        public AllowedStatusAttribute(string setName)
        {
            _setName = setName;
            ErrorMessage = "{0} không hợp lệ (chỉ chấp nhận: {1}).";
        }

        public override string FormatErrorMessage(string name) =>
            string.Format(ErrorMessageString, name, string.Join(", ", StatusSets.Get(_setName)));

        public override bool IsValid(object? value) =>
            value is null || (value is string s && StatusSets.IsValid(StatusSets.Get(_setName), s));
    }

    /// <summary>Ngày hợp lệ cho SQL Server DATETIME (>= 1753) và hợp lý nghiệp vụ (2000–2100).</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class SqlDateRangeAttribute : ValidationAttribute
    {
        public static readonly DateTime Min = new(2000, 1, 1);
        public static readonly DateTime Max = new(2100, 12, 31);

        public SqlDateRangeAttribute() => ErrorMessage = "{0} không hợp lệ (phải trong khoảng năm 2000–2100).";

        public override bool IsValid(object? value) => value switch
        {
            null => true,
            DateTime d => d >= Min && d <= Max,
            DateOnly d => d.ToDateTime(TimeOnly.MinValue) >= Min && d.ToDateTime(TimeOnly.MinValue) <= Max,
            _ => true
        };
    }

    /// <summary>Lỗi dữ liệu không hợp lệ bị chặn ngay trước khi SaveChanges.</summary>
    public class DataValidationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }
        public DataValidationException(IEnumerable<string> errors)
            : base("Dữ liệu không hợp lệ: " + string.Join(" | ", errors))
            => Errors = errors.ToList();
    }

    public static class ModelValidation
    {
        /// <summary>
        /// Validator.TryValidateObject không tự đọc [MetadataType] — phải đăng ký provider.
        /// Gọi một lần lúc khởi động (Program.cs).
        /// </summary>
        public static void RegisterMetadata()
        {
            foreach (var t in typeof(ModelValidation).Assembly.GetTypes())
            {
                var attr = t.GetCustomAttribute<MetadataTypeAttribute>();
                if (attr == null) continue;
                TypeDescriptor.AddProviderTransparent(
                    new AssociatedMetadataTypeTypeDescriptionProvider(t, attr.MetadataClassType), t);
            }
        }

        /// <summary>Validate một object, trả về danh sách lỗi (rỗng nếu hợp lệ).</summary>
        public static List<string> Validate(object entity)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(entity, new ValidationContext(entity), results, validateAllProperties: true);
            return results.Select(r => r.ErrorMessage ?? "Dữ liệu không hợp lệ.").ToList();
        }
    }

    /// <summary>
    /// Kiểm tra dữ liệu người dùng nhập ở tầng page-handler (trước khi gán vào entity),
    /// để trả thông báo thân thiện. Metadata + SaveChanges là lớp chặn cuối cùng.
    /// </summary>
    public static class InputValidator
    {
        /// <summary>Trim, chuyển chuỗi rỗng thành null.</summary>
        public static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        public static string? Text(string? v, string label, int max, bool required = false)
        {
            var s = Clean(v);
            if (s == null) return required ? $"{label} là bắt buộc." : null;
            return s.Length > max ? $"{label} không được vượt quá {max} ký tự." : null;
        }

        public static string? Email(string? v, string label = "Email")
        {
            var s = Clean(v);
            if (s == null) return null;
            if (s.Length > 100) return $"{label} không được vượt quá 100 ký tự.";
            return new EmailAddressAttribute().IsValid(s) ? null : $"{label} không đúng định dạng.";
        }

        public static string? Phone(string? v, string label = "Số điện thoại")
        {
            var s = Clean(v);
            if (s == null) return null;
            return Regex.IsMatch(s, ValidationPatterns.Phone) ? null : $"{label} không hợp lệ (8–20 ký tự: số, +, -, khoảng trắng, dấu ngoặc).";
        }

        public static string? Username(string? v)
        {
            var s = Clean(v);
            if (s == null) return "Tên đăng nhập là bắt buộc.";
            return Regex.IsMatch(s, ValidationPatterns.Username) ? null : "Tên đăng nhập chỉ gồm chữ, số, dấu . _ - (3–50 ký tự).";
        }

        public static string? Password(string? v, string label = "Mật khẩu")
        {
            if (string.IsNullOrEmpty(v)) return $"{label} là bắt buộc.";
            if (v.Length < 6) return $"{label} phải có ít nhất 6 ký tự.";
            if (v.Length > 100) return $"{label} không được vượt quá 100 ký tự.";
            return Regex.IsMatch(v, ValidationPatterns.AsciiPassword) ? null : $"{label} chỉ được dùng ký tự ASCII (không dấu) và không bắt đầu/kết thúc bằng khoảng trắng.";
        }

        public static string? Status(string? v, string[] set, string label = "Trạng thái") =>
            string.IsNullOrWhiteSpace(v) || set.Contains(v.Trim(), StringComparer.Ordinal)
                ? null : $"{label} không hợp lệ.";

        /// <summary>Trả về lỗi đầu tiên khác null.</summary>
        public static string? First(params string?[] errors) => errors.FirstOrDefault(e => e != null);
    }
}
