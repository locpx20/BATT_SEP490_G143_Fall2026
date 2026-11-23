using System.Text;
using BATT_PPMS.Services;
using Xunit;

namespace BATT_PPMS.UnitTests.Services;

public class QcEvidenceAndPlanningTests
{
    [Fact]
    public void QcEvidence_RecognizesPngSignature()
    {
        var bytes = new byte[12];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        Assert.Equal("image/png", QcEvidence.Validate(bytes));
    }

    [Fact]
    public void QcEvidence_RecognizesJpegSignature()
    {
        var bytes = new byte[12];
        bytes[0] = 255;
        bytes[1] = 216;
        bytes[2] = 255;
        Assert.Equal("image/jpeg", QcEvidence.Validate(bytes));
    }

    [Fact]
    public void QcEvidence_RecognizesWebpSignature()
    {
        var bytes = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);
        Assert.Equal("image/webp", QcEvidence.Validate(bytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    [InlineData(new byte[11])]
    public void QcEvidence_MissingOrTooSmallData_Throws(byte[]? bytes)
    {
        var exception = Assert.Throws<ProductionFlowException>(() => QcEvidence.Validate(bytes));
        Assert.Contains("ảnh kết quả", exception.Message);
    }

    [Fact]
    public void QcEvidence_UnsupportedFileSignature_Throws()
    {
        var bytes = Encoding.ASCII.GetBytes("NOT_AN_IMAGE");
        var exception = Assert.Throws<ProductionFlowException>(() => QcEvidence.Validate(bytes));
        Assert.Contains("JPG, PNG hoặc WebP", exception.Message);
    }

    [Fact]
    public void QcEvidence_TooLargeFile_Throws()
    {
        var bytes = new byte[QcEvidence.MaxBytes + 1];
        var exception = Assert.Throws<ProductionFlowException>(() => QcEvidence.Validate(bytes));
        Assert.Contains("tối đa 5 MB", exception.Message);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-0.01, false)]
    [InlineData(0.01, true)]
    [InlineData(1000, true)]
    [InlineData(9999999999.99, true)]
    [InlineData(10000000000, false)]
    [InlineData(1.234, false)]
    [InlineData(1.23, true)]
    public void DailyMaterialPlanning_ValidQuantity_EnforcesRangeAndTwoDecimals(decimal kg, bool expected) =>
        Assert.Equal(expected, DailyMaterialPlanning.ValidQuantity(kg));
}