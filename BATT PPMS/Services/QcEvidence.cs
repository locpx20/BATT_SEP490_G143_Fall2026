namespace BATT_PPMS.Services;

public static class QcEvidence
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public static string Validate(byte[]? data) {
        if(data == null || data.Length < 12 || data.Length > MaxBytes)
            throw new ProductionFlowException("Vui lòng đính kèm ảnh kết quả kiểm tra, tối đa 5 MB.");
        if(data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})) return "image/png";
        if(data[0] == 255 && data[1] == 216 && data[2] == 255) return "image/jpeg";
        if(System.Text.Encoding.ASCII.GetString(data,0,4)=="RIFF" && System.Text.Encoding.ASCII.GetString(data,8,4)=="WEBP") return "image/webp";
        throw new ProductionFlowException("Ảnh kết quả phải là JPG, PNG hoặc WebP hợp lệ.");
    }
}
