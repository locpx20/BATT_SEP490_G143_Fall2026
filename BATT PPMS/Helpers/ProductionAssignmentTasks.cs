namespace BATT_PPMS.Helpers;

public static class ProductionAssignmentTasks
{
    public const string Chipper = "Máy băm";

    public static bool IsChipping(string? task) => MachineName(task) == Chipper;

    public static string MachineName(string? task)
    {
        var description = task?.ToLowerInvariant() ?? string.Empty;
        if (description.Contains("đóng gói") || description.Contains("đóng bao")) return "Đóng gói";
        if (description.Contains("sấy")) return "Máy sấy";
        if (description.Contains("nén") || description.Contains("ép viên")) return "Máy nén";
        if (description.Contains("băm")) return Chipper;
        if (description.Contains("nghiền")) return "Máy nghiền";
        return task ?? string.Empty;
    }
}
