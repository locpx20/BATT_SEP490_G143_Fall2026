using BATT_PPMS.Services;
using Xunit;

namespace BATT_PPMS.UnitTests.Services;

public class ProductionFlowLabelTests
{
    [Theory]
    [InlineData("Pending", "Chờ tiếp nhận")]
    [InlineData("In Progress", "Đang kiểm tra")]
    [InlineData("Completed", "Đã trả kết quả")]
    [InlineData("Pass", "Đạt")]
    [InlineData("Fail", "Không đạt")]
    [InlineData("Rejected", "Bị từ chối")]
    [InlineData("Issued", "Đã cấp nguyên liệu")]
    [InlineData("Received", "Đã nhập kho")]
    [InlineData("AwaitingStaffConfirmation", "Đã nhập kho")]
    [InlineData("Submitted", "Chờ Manager duyệt")]
    [InlineData("Approved", "Đã duyệt")]
    [InlineData("Other", "Other")]
    [InlineData(null, "Chưa có")]
    public void Label_MapsStatusToDisplayText(string? status, string expected) =>
        Assert.Equal(expected, ProductionFlow.Label(status));

    [Theory]
    [InlineData("Pending", "Chờ kiểm tra")]
    [InlineData("Completed", "Đã trả kết quả")]
    [InlineData(null, "Chưa có")]
    public void QcStatusLabel_UsesQcSpecificPendingLabel(string? status, string expected) =>
        Assert.Equal(expected, ProductionFlow.QcStatusLabel(status));
}