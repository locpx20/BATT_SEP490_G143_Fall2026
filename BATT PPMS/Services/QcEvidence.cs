using System.IO.Compression;

namespace BATT_PPMS.Services;

public static class QcEvidence
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxImages = 10;
    public const string CollectionType = "application/x-qc-images";

    public static (byte[] Data, string Type) Pack(IReadOnlyList<byte[]> images)
    {
        if (images.Count == 0 || images.Count > MaxImages)
            throw new ProductionFlowException($"Vui lòng chọn từ 1 đến {MaxImages} ảnh kết quả.");
        var types = images.Select(Validate).ToArray();
        if (images.Count == 1) return (images[0], types[0]);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            for (var i = 0; i < images.Count; i++)
            {
                var entry = archive.CreateEntry(i.ToString("D2"), CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(images[i]);
            }
        }
        return (output.ToArray(), CollectionType);
    }

    public static int Count(byte[]? data, string? type)
    {
        if (data == null) return 0;
        if (type != CollectionType) return 1;
        using var archive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        return archive.Entries.Count;
    }

    public static (byte[] Data, string Type)? Get(byte[]? data, string? type, int index)
    {
        if (data == null || index < 0) return null;
        if (type != CollectionType) return index == 0 ? (data, type ?? "application/octet-stream") : null;
        using var archive = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        if (index >= archive.Entries.Count) return null;
        using var entry = archive.Entries[index].Open();
        using var output = new MemoryStream();
        entry.CopyTo(output);
        var image = output.ToArray();
        return (image, Validate(image));
    }
    public static string Validate(byte[]? data) {
        if(data == null || data.Length < 12 || data.Length > MaxBytes)
            throw new ProductionFlowException("Vui lòng đính kèm ảnh kết quả kiểm tra, tối đa 5 MB.");
        if(data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})) return "image/png";
        if(data[0] == 255 && data[1] == 216 && data[2] == 255) return "image/jpeg";
        if(System.Text.Encoding.ASCII.GetString(data,0,4)=="RIFF" && System.Text.Encoding.ASCII.GetString(data,8,4)=="WEBP") return "image/webp";
        throw new ProductionFlowException("Ảnh kết quả phải là JPG, PNG hoặc WebP hợp lệ.");
    }
}
