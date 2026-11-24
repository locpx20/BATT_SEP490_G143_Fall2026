using System.Text.Json;
using System.Text.RegularExpressions;

namespace BATT_PPMS.Helpers
{
    public class StaffAssignmentNote
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class InboundDeliveryInfo
    {
        public int ReceiptId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string DeliveryTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Schema mới không còn các cột RejectionReason / CompletionNote.
    /// Lý do từ chối, ghi chú nghiệm thu, danh sách phân công nhiều nhân viên thu mua
    /// và thông tin vận chuyển yêu cầu nhập kho được lưu thành dòng có gắn thẻ
    /// ("[REJECTED] ...", "[COMPLETED] ...", "[ASSIGNMENTS] ...", "[IR_DELIVERIES] ...")
    /// ngay trong cột Note / Description sẵn có.
    /// </summary>
    public static class NoteTags
    {
        public const string Rejected = "REJECTED";
        public const string Completed = "COMPLETED";
        public const string Assignments = "ASSIGNMENTS";
        public const string InboundDeliveries = "IR_DELIVERIES";

        private static Regex LineOf(string tag) =>
            new($@"^\[{Regex.Escape(tag)}\][ ]?(.*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>Ghi (hoặc thay) dòng gắn thẻ trong text hiện có.</summary>
        public static string Set(string? existing, string tag, string value)
        {
            var clean = Remove(existing, tag);
            var line = $"[{tag}] {value.Replace("\r", " ").Replace("\n", " ").Trim()}";
            return string.IsNullOrWhiteSpace(clean) ? line : clean + "\n" + line;
        }

        /// <summary>Lấy nội dung của dòng gắn thẻ, null nếu không có.</summary>
        public static string? Get(string? existing, string tag)
        {
            if (string.IsNullOrEmpty(existing)) return null;
            var m = LineOf(tag).Match(existing.Replace("\r", ""));
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        /// <summary>Xoá dòng gắn thẻ khỏi text (trả về null nếu rỗng).</summary>
        public static string? Remove(string? existing, string tag)
        {
            if (string.IsNullOrEmpty(existing)) return existing;
            var t = Regex.Replace(LineOf(tag).Replace(existing.Replace("\r", ""), ""), "\n{2,}", "\n").Trim('\n', ' ');
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        /// <summary>Text hiển thị cho người dùng: bỏ mọi dòng gắn thẻ.</summary>
        public static string? Plain(string? existing) =>
            Remove(Remove(Remove(Remove(existing, Rejected), Completed), Assignments), InboundDeliveries);

        public static List<InboundDeliveryInfo> GetInboundDeliveries(string? existing)
        {
            var raw = Get(existing, InboundDeliveries);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<InboundDeliveryInfo>>(raw);
                    if (parsed != null)
                        return parsed;
                }
                catch
                {
                    // Ignore malformed JSON
                }
            }
            return new List<InboundDeliveryInfo>();
        }

        public static InboundDeliveryInfo? GetInboundDelivery(string? existing, int receiptId) =>
            GetInboundDeliveries(existing).FirstOrDefault(x => x.ReceiptId == receiptId);

        public static string SetInboundDelivery(string? existing, InboundDeliveryInfo item)
        {
            var list = GetInboundDeliveries(existing);
            list.RemoveAll(x => x.ReceiptId == item.ReceiptId);
            list.Add(item);
            return Set(existing, InboundDeliveries, JsonSerializer.Serialize(list));
        }

        public static string SetAssignments(string? existing, List<StaffAssignmentNote> items)
        {
            var json = JsonSerializer.Serialize(items);
            return Set(existing, Assignments, json);
        }

        public static List<StaffAssignmentNote> GetAssignments(string? existing, int? fallbackStaffId = null, string? fallbackStaffName = null)
        {
            var raw = Get(existing, Assignments);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<StaffAssignmentNote>>(raw);
                    if (parsed != null && parsed.Count > 0)
                        return parsed;
                }
                catch
                {
                    // Ignore malformed JSON and fall back
                }
            }

            if (fallbackStaffId.HasValue && fallbackStaffId.Value > 0)
            {
                return new List<StaffAssignmentNote>
                {
                    new StaffAssignmentNote
                    {
                        UserId = fallbackStaffId.Value,
                        FullName = string.IsNullOrWhiteSpace(fallbackStaffName) ? $"NV #{fallbackStaffId.Value}" : fallbackStaffName,
                        Note = null
                    }
                };
            }

            return new List<StaffAssignmentNote>();
        }

        public static bool IsStaffAssigned(string? existingNote, int? assignedTo, int? createdBy, int userId)
        {
            var list = GetAssignments(existingNote, assignedTo ?? createdBy);
            if (list.Any(a => a.UserId == userId)) return true;
            return (assignedTo ?? createdBy) == userId;
        }
    }
}
