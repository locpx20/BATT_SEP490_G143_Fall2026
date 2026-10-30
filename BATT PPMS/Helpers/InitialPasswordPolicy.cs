namespace BATT_PPMS.Helpers;

public static class InitialPasswordPolicy
{
    public const string DefaultPassword = "123456";

    public static bool RequiresChange(string? role, string? password) =>
        role is not RoleConstants.Admin and not RoleConstants.AdminShort &&
        password == DefaultPassword;

    public static string? ValidateNewPassword(string? password, string? confirmation)
    {
        var error = InputValidator.Password(password, "Mật khẩu mới");
        if (error != null) return error;
        if (password == DefaultPassword)
            return "Mật khẩu mới phải khác mật khẩu mặc định 123456.";
        return password == confirmation ? null : "Xác nhận mật khẩu không khớp.";
    }
}
