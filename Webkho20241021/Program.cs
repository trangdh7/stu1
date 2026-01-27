using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Webkho_20241021.Models;
using OfficeOpenXml;
using System.Linq;
using System.Security.Claims;
using Webkho_20241021.Helpers;
using Microsoft.AspNetCore.Http.Features;
using Webkho_20241021.Services;

// Cấu hình license cho EPPlus 8+
ExcelPackage.License.SetNonCommercialPersonal("Webkho Management System");

// Khởi tạo builder
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

// Cấu hình URL trực tiếp (khuyến nghị để tránh lỗi Collection fixed-size)
builder.WebHost.UseUrls("http://*:80");

// Cấu hình kết nối đến cơ sở dữ liệu
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("MySQLConnection")));

builder.Services.AddDbContext<ApplicationDbContext_user>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("MySQLConnection")));

// Thêm dịch vụ vào container
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Đăng ký EmailService và provider cấu hình email động
builder.Services.AddScoped<IEmailSettingsProvider, EmailSettingsProvider>();
builder.Services.AddScoped<Webkho_20241021.Services.EmailService>();
builder.Services.AddScoped<IYeucauCodeService, YeucauCodeService>();

// Cấu hình FormOptions để tăng giới hạn form values (hỗ trợ upload file Excel lớn)
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 20000; // Tăng từ 1024 (mặc định) lên 20000
    options.KeyLengthLimit = 2048;   // Tăng giới hạn độ dài key
    options.ValueLengthLimit = 4194304; // 4MB cho mỗi value
    options.MultipartBodyLengthLimit = 134217728; // 128MB cho toàn bộ request
});

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext_user>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/Dangnhap";
});

// Cấu hình Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Xây dựng ứng dụng
var app = builder.Build();

// Middleware để ngăn cache
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Cấu hình xử lý lỗi 404 và các status code khác (phải đặt sau UseRouting)
app.UseStatusCodePagesWithReExecute("/Home/NotFound");

// Middleware để log authorization failures
app.Use(async (context, next) =>
{
    await next();
    
    // Log khi có redirect về login (có thể do authorization failure)
    if (context.Response.StatusCode == 302 || context.Response.StatusCode == 401)
    {
        var path = context.Request.Path.Value;
        if (path != null && path.Contains("/Home/Dangnhap"))
        {
            Console.WriteLine($"⚠️ Authorization failure or redirect to login. Path: {context.Request.Path}, Status: {context.Response.StatusCode}");
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var roles = context.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
                Console.WriteLine($"   User authenticated: {context.User.Identity.Name}, Roles in claims: [{string.Join(", ", roles)}]");
            }
            else
            {
                Console.WriteLine($"   User NOT authenticated");
            }
        }
    }
});

// Cấu hình route cho Areas
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Trangchu}/{id?}");

// Cấu hình route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Dangnhap}/{id?}");

// ✅ Kiểm tra kết nối MySQL và cấu hình Email khi khởi động ứng dụng
using (var scope = app.Services.CreateScope())
{       
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (db.Database.CanConnect())
            Console.WriteLine("✅ Đã kết nối thành công tới MySQL!");
        else
            Console.WriteLine("❌ Không thể kết nối tới MySQL!");

        MakhoHelper.EnsurePlaceholderKho(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine("⚠️ Lỗi kết nối MySQL: " + ex.Message);
    }
    
    // Kiểm tra cấu hình Email
    try
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var fromEmail = config["EmailSettings:FromEmail"];
        var fromPassword = config["EmailSettings:FromPassword"];
        
        Console.WriteLine("📧 Kiểm tra cấu hình Email:");
        Console.WriteLine($"   FromEmail: {fromEmail ?? "(null)"}");
        Console.WriteLine($"   FromPassword: {(string.IsNullOrEmpty(fromPassword) ? "(null hoặc rỗng)" : $"Đã có (độ dài: {fromPassword.Length})")}");
        
        if (string.IsNullOrEmpty(fromPassword))
        {
            Console.WriteLine("⚠️ CẢNH BÁO: FromPassword không được cấu hình trong appsettings.json!");
            Console.WriteLine("   Vui lòng kiểm tra và cập nhật password trong file appsettings.json");
        }
        else
        {
            Console.WriteLine("✅ Cấu hình Email đã được thiết lập đúng");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("⚠️ Lỗi kiểm tra cấu hình Email: " + ex.Message);
    }
}

// Chạy ứng dụng
        app.Run();
