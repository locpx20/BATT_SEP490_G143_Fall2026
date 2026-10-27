using System.Text.RegularExpressions;

namespace BATT_PPMS.Helpers
{
    /// <summary>
    /// Schema mới không còn các cột RejectionReason / CompletionNote.
    /// Lý do từ chối và ghi chú nghiệm thu được lưu thành một dòng có gắn thẻ
    /// ("[REJECTED] ...", "[COMPLETED] ...") ngay trong cột Note / Description sẵn có.
    /// </summary>
    public static class NoteTags
    {
        public const string Rejected = "REJECTED";
        public const string Completed = "COMPLETED";

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
            Remove(Remove(existing, Rejected), Completed);
    }
}
