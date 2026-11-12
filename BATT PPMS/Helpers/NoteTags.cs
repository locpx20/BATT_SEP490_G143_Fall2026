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

    /// <summary>
    /// Schema mới không còn các cột RejectionReason / CompletionNote.
    /// Lý do từ chối, ghi chú nghiệm thu và danh sách phân công nhiều nhân viên thu mua
    /// được lưu thành dòng có gắn thẻ ("[REJECTED] ...", "[COMPLETED] ...", "[ASSIGNMENTS] ...")
    /// ngay trong cột Note / Description sẵn có.
    /// </summary>
    public static class NoteTags
    {
        public const string Rejected = "REJECTED";
        public const string Completed = "COMPLETED";
        public const string Assignments = "ASSIGNMENTS";

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
            Remove(Remove(Remove(existing, Rejected), Completed), Assignments);

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
