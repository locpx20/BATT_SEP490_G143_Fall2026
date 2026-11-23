using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class PolicyAndMappingTests
{
    [Fact]
    public void RequiresChange_DefaultPasswordForNonAdmin_ReturnsTrue() =>
        Assert.True(InitialPasswordPolicy.RequiresChange("Manager", PasswordHasher.Hash(InitialPasswordPolicy.DefaultPassword)));

    [Theory]
    [InlineData("Admin")]
    [InlineData("Admin System")]
    public void RequiresChange_AdminRole_ReturnsFalse(string role) =>
        Assert.False(InitialPasswordPolicy.RequiresChange(role, PasswordHasher.Hash(InitialPasswordPolicy.DefaultPassword)));

    [Fact]
    public void RequiresChange_NonDefaultPassword_ReturnsFalse() =>
        Assert.False(InitialPasswordPolicy.RequiresChange("Manager", PasswordHasher.Hash("newPassword")));

    [Fact]
    public void RequiresChange_NullPassword_ReturnsFalse() =>
        Assert.False(InitialPasswordPolicy.RequiresChange("Manager", null));

    [Fact]
    public void ValidateNewPassword_ValidAndMatchingPassword_ReturnsNull() =>
        Assert.Null(InitialPasswordPolicy.ValidateNewPassword("NewPass123", "NewPass123"));

    [Fact]
    public void ValidateNewPassword_DefaultPassword_IsRejected() =>
        Assert.Equal("Mật khẩu mới phải khác mật khẩu mặc định 123456.", InitialPasswordPolicy.ValidateNewPassword("123456", "123456"));

    [Fact]
    public void ValidateNewPassword_Mismatch_IsRejected() =>
        Assert.Equal("Xác nhận mật khẩu không khớp.", InitialPasswordPolicy.ValidateNewPassword("NewPass123", "OtherPass123"));

    [Fact]
    public void ValidateNewPassword_InvalidPassword_ReturnsPasswordValidationError() =>
        Assert.Equal("Mật khẩu mới phải có ít nhất 6 ký tự.", InitialPasswordPolicy.ValidateNewPassword("123", "123"));

    [Theory]
    [InlineData("Đóng gói viên")]
    [InlineData("Đóng bao thành phẩm")]
    public void MachineName_PackagingTask_ReturnsPackaging(string task) =>
        Assert.Equal("Đóng gói", ProductionAssignmentTasks.MachineName(task));

    [Theory]
    [InlineData("Công đoạn sấy", "Máy sấy")]
    [InlineData("Ép viên gỗ", "Máy nén")]
    [InlineData("Băm nguyên liệu", "Máy băm")]
    [InlineData("Nghiền nguyên liệu", "Máy nghiền")]
    [InlineData("Kiểm tra", "Kiểm tra")]
    public void MachineName_MapsKnownTask(string task, string expected) =>
        Assert.Equal(expected, ProductionAssignmentTasks.MachineName(task));

    [Fact]
    public void MachineName_NullTask_ReturnsEmpty() =>
        Assert.Equal(string.Empty, ProductionAssignmentTasks.MachineName(null));

    [Fact]
    public void IsChipping_RecognizesChippingTask()
    {
        Assert.True(ProductionAssignmentTasks.IsChipping("Băm nguyên liệu"));
        Assert.False(ProductionAssignmentTasks.IsChipping("Nghiền nguyên liệu"));
    }

    [Theory]
    [InlineData("Admin", "Admin")]
    [InlineData("Owner", "Owner")]
    [InlineData("Manager", "Manager")]
    [InlineData("Purchasing Staff", "Purchasing")]
    [InlineData("Warehouse Staff", "Warehouse")]
    [InlineData("Staff", "Production")]
    [InlineData("QC Staff", "QualityControl")]
    [InlineData("Maintenance Staff", "Maintenance")]
    public void RoleViews_Folder_MapsKnownRoles(string role, string expected) =>
        Assert.Equal(expected, RoleViews.Folder(role));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void RoleViews_UnknownRole_ReturnsNull(string? role) =>
        Assert.Null(RoleViews.Folder(role));

    [Fact]
    public void RoleViews_IsWorkspaceRole_ExcludesAdmin()
    {
        Assert.False(RoleViews.IsWorkspaceRole("Admin"));
        Assert.True(RoleViews.IsWorkspaceRole("Manager"));
        Assert.False(RoleViews.IsWorkspaceRole("Unknown"));
    }

    [Theory]
    [InlineData(" FullName ", "fullname")]
    [InlineData("USERNAME", "username")]
    [InlineData("Phone", "phone")]
    [InlineData(null, "all")]
    [InlineData("invalid", "all")]
    public void UserSearch_NormalizeField_ReturnsSupportedField(string? field, string expected) =>
        Assert.Equal(expected, UserSearch.NormalizeField(field));
}