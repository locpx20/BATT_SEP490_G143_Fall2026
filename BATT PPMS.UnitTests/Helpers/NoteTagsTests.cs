using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class NoteTagsTests
{
    [Fact]
    public void Set_AddsTaggedLineToExistingText()
    {
        var result = NoteTags.Set("Ghi chú ban đầu", NoteTags.Rejected, "Thiếu thông tin");
        Assert.Equal("Ghi chú ban đầu\n[REJECTED] Thiếu thông tin", result);
    }

    [Fact]
    public void Set_ReplacesExistingTagInsteadOfDuplicating()
    {
        var result = NoteTags.Set("[REJECTED] Lý do cũ\nNội dung khác", NoteTags.Rejected, "Lý do mới");
        Assert.Equal("Nội dung khác\n[REJECTED] Lý do mới", result);
    }

    [Fact]
    public void Set_NormalizesNewLinesInTagValue()
    {
        var result = NoteTags.Set(null, NoteTags.Completed, "Đã sửa\nxong");
        Assert.Equal("[COMPLETED] Đã sửa xong", result);
    }

    [Fact]
    public void Get_ReturnsTaggedValue()
    {
        Assert.Equal("Không đạt", NoteTags.Get("Ghi chú\n[REJECTED] Không đạt", NoteTags.Rejected));
    }

    [Fact]
    public void Get_MissingTag_ReturnsNull() => Assert.Null(NoteTags.Get("Ghi chú", NoteTags.Rejected));

    [Fact]
    public void Remove_RemovesOnlyRequestedTag()
    {
        var source = "Ghi chú\n[REJECTED] Lý do\n[COMPLETED] Nghiệm thu";
        Assert.Equal("Ghi chú\n[COMPLETED] Nghiệm thu", NoteTags.Remove(source, NoteTags.Rejected));
    }

    [Fact]
    public void Remove_WhenOnlyTagExists_ReturnsNull()
    {
        Assert.Null(NoteTags.Remove("[REJECTED] Lý do", NoteTags.Rejected));
    }

    [Fact]
    public void Plain_RemovesAllInternalTags()
    {
        var source = "Nội dung\n[REJECTED] Lý do\n[COMPLETED] Ghi nhận\n[ASSIGNMENTS] []";
        Assert.Equal("Nội dung", NoteTags.Plain(source));
    }

    [Fact]
    public void SetAssignments_AndGetAssignments_RoundTripsItems()
    {
        var items = new List<StaffAssignmentNote>
        {
            new() { UserId = 10, FullName = "Nguyễn Văn A", Note = "Ca sáng" },
            new() { UserId = 11, FullName = "Trần Văn B", Note = null }
        };
        var note = NoteTags.SetAssignments("Ghi chú", items);
        var actual = NoteTags.GetAssignments(note);
        Assert.Equal(2, actual.Count);
        Assert.Equal(10, actual[0].UserId);
        Assert.Equal("Nguyễn Văn A", actual[0].FullName);
        Assert.Equal("Ca sáng", actual[0].Note);
        Assert.Equal(11, actual[1].UserId);
    }

    [Fact]
    public void GetAssignments_MalformedJson_UsesFallbackStaff()
    {
        var actual = NoteTags.GetAssignments("[ASSIGNMENTS] {invalid", 7, "Nhân viên 7");
        var item = Assert.Single(actual);
        Assert.Equal(7, item.UserId);
        Assert.Equal("Nhân viên 7", item.FullName);
    }

    [Fact]
    public void GetAssignments_WithoutDataOrFallback_ReturnsEmptyList()
    {
        Assert.Empty(NoteTags.GetAssignments("Ghi chú thông thường"));
    }

    [Fact]
    public void IsStaffAssigned_RecognizesStaffInAssignments()
    {
        var note = NoteTags.SetAssignments(null, new() { new() { UserId = 20, FullName = "Staff 20" } });
        Assert.True(NoteTags.IsStaffAssigned(note, null, null, 20));
        Assert.False(NoteTags.IsStaffAssigned(note, null, null, 21));
    }
}