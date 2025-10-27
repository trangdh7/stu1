using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình kết nối đến cơ sở dữ liệu
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("MySQLConnection")));

builder.Services.AddDbContext<ApplicationDbContext_user>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("MySQLConnection")));

// Thêm dịch vụ vào container
builder.Services.AddControllersWithViews();

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

// Cấu hình middleware để ngăn cache
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

app.Urls.Add("http://*:80");
//app.Urls.Add("https://*:7001");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication(); // Kích hoạt Authentication
app.UseAuthorization();

// Cấu hình route cho Areas
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Trangchu}/{id?}");

// Cấu hình route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Dangnhap}/{id?}");

// ✅ KIỂM TRA KẾT NỐI MYSQL NGAY KHI KHỞI ĐỘNG ỨNG DỤNG
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (db.Database.CanConnect())
            Console.WriteLine("✅ Đã kết nối thành công tới MySQL!");
        else
            Console.WriteLine("❌ Không thể kết nối tới MySQL!");
    }
    catch (Exception ex)
    {
        Console.WriteLine("⚠️ Lỗi kết nối MySQL: " + ex.Message);
    }
}

// Chạy ứng dụng
app.Run();
