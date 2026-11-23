using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class WorkflowMappingTests
{
    [Theory]
    [InlineData(null, RepairStatus.Pending)]
    [InlineData("", RepairStatus.Pending)]
    [InlineData("Reported", RepairStatus.Pending)]
    [InlineData("In Progress", RepairStatus.InProgress)]
    [InlineData("Completed", RepairStatus.Completed)]
    public void RepairStatus_Normalize_MapsLegacyAndKnownValues(string? input, string expected) =>
        Assert.Equal(expected, RepairStatus.Normalize(input));

    [Theory]
    [InlineData(RepairStatus.Pending, "Chờ duyệt")]
    [InlineData(RepairStatus.Rejected, "Bị từ chối – chờ chỉnh sửa")]
    [InlineData(RepairStatus.Assigned, "Đã phân công – chờ xác nhận")]
    [InlineData(RepairStatus.InProgress, "Đang sửa chữa")]
    [InlineData(RepairStatus.WaitingMaterial, "Chờ vật tư")]
    [InlineData(RepairStatus.Completed, "Đã hoàn thành")]
    [InlineData(RepairStatus.Cancelled, "Đã hủy")]
    public void RepairStatus_Label_ReturnsVietnameseLabel(string status, string expected) =>
        Assert.Equal(expected, RepairStatus.Label(status));

    [Fact]
    public void RepairStatus_Equivalents_ReturnsLegacyStatusAliases()
    {
        Assert.Equal(new[] { "Pending", "Reported" }, RepairStatus.Equivalents(RepairStatus.Pending));
        Assert.Equal(new[] { "InProgress", "In Progress" }, RepairStatus.Equivalents(RepairStatus.InProgress));
    }

    [Theory]
    [InlineData("Low", "Thấp")]
    [InlineData("Normal", "Bình thường")]
    [InlineData("Medium", "Trung bình")]
    [InlineData("High", "Cao")]
    [InlineData("Critical", "Khẩn cấp")]
    [InlineData(null, "—")]
    public void RepairStatus_PriorityLabel_ReturnsExpectedLabel(string? priority, string expected) =>
        Assert.Equal(expected, RepairStatus.PriorityLabel(priority));

    [Fact]
    public void RepairAction_Label_MapsKnownAction()
    {
        Assert.Equal("Tạo yêu cầu sửa chữa", RepairAction.Label(RepairAction.Created));
        Assert.Equal("Hoàn thành sửa chữa", RepairAction.Label(RepairAction.Completed));
    }

    [Fact]
    public void RepairAction_Icon_MapsKnownAction()
    {
        Assert.Equal("bi-plus-circle-fill", RepairAction.Icon(RepairAction.Created));
        Assert.Equal("bi-circle-fill", RepairAction.Icon("Unknown"));
    }

    [Fact]
    public void PurchaseRequestStatus_Normalize_MapsLegacyApprovedToPurchased()
    {
        Assert.Equal(PurchaseRequestStatus.Pending, PurchaseRequestStatus.Normalize(null));
        Assert.Equal(PurchaseRequestStatus.Purchased, PurchaseRequestStatus.Normalize("Approved"));
    }

    [Fact]
    public void PurchaseRequestStatus_Equivalents_ReturnsLegacyAlias()
    {
        Assert.Equal(new[] { "Purchased", "Approved" },
            PurchaseRequestStatus.Equivalents(PurchaseRequestStatus.Purchased));
    }

    [Fact]
    public void MachineStatusGuard_MarkDown_SetsMachineDown()
    {
        var machine = new BATT_PPMS.Models.Machine();
        MachineStatusGuard.MarkDown(machine);
        Assert.Equal(MachineStatusGuard.Down, machine.Status);
    }
}