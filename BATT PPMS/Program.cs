using Microsoft.EntityFrameworkCore;
using BATT_PPMS.Data;

namespace BATT_PPMS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();

            // Entity Framework Core - SQL Server
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Session (dùng cho đăng nhập đơn giản)
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(8);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // HttpContext Accessor
            builder.Services.AddHttpContextAccessor();

            // In-Memory Cache cho OTP
            builder.Services.AddMemoryCache();

            // Email Service (thay thế Zalo) & OTP Service
            builder.Services.AddSingleton<BATT_PPMS.Services.IEmailService, BATT_PPMS.Services.EmailService>();
            builder.Services.AddSingleton<BATT_PPMS.Services.IOtpService, BATT_PPMS.Services.OtpService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthorization();
            app.MapRazorPages();
            app.Run();
        }
    }
}
