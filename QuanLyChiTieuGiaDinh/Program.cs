using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
//using QuanLyChiTieuGiaDinh.Services;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// KẾT NỐI DATABASE
// ========================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));


// ========================================
// HIỂN THỊ LỖI DATABASE KHI PHÁT TRIỂN
// ========================================

builder.Services.AddDatabaseDeveloperPageExceptionFilter();


// ========================================
// ASP.NET CORE IDENTITY
// ========================================

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();


// ========================================
// MVC & SERVICES
// ========================================

builder.Services.AddControllersWithViews();

//builder.Services.AddScoped<GiaoDichDinhKyService>();
//builder.Services.AddScoped<ThongBaoService>();
//builder.Services.AddScoped<NhatKyHeThongService>();
//builder.Services.AddHttpClient<QuanLyChiTieuGiaDinh.Services.GeminiOcrService>(client =>
////{
//    client.Timeout = TimeSpan.FromMinutes(5);
//});
//builder.Services.AddHostedService<TuDongLapNganSachService>();

// ========================================
// DỊCH VỤ OCR (FPT.AI)
// ========================================

//builder.Services.AddHttpClient<FptAiOcrService>(client =>
//{
//    client.Timeout = TimeSpan.FromSeconds(30); // Tránh bị treo request khi OCR phản hồi chậm
//});

// ========================================
// BUILD APP
// ========================================

var app = builder.Build();


// ========================================
// MIGRATION & SEED DỮ LIỆU MẪU
// ========================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Tự động apply Migration mới nhất vào SQL Server
        await context.Database.MigrateAsync();

        // Chạy hàm Seed Dữ liệu mẫu
        //await SeedData.SeedAsync(context);

        //logger.LogInformation(" Seed dữ liệu mẫu thành công!");
    }
    catch (DbUpdateException ex)
    {
        // In chi tiết nguyên nhân gốc rễ từ SQL Server nếu có lỗi CSDL
        var innerException = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        logger.LogError(ex, " Lỗi DbUpdateException khi Seed Data: {ErrorMessage}", innerException);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, " Lỗi hệ thống khi khởi tạo Database!");
    }
}


// ========================================
// HTTP REQUEST PIPELINE
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


// ========================================
// HTTPS + STATIC FILES
// ========================================

app.UseHttpsRedirection();
app.UseStaticFiles();


// ========================================
// ROUTING & IDENTITY PIPELINE
// ========================================

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


// ========================================
// ROUTES CONFIGURATION
// ========================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TrangChu}/{action=TrangChinh}/{id?}");

app.MapRazorPages();


// ========================================
// RUN APPLICATION
// ========================================

app.Run();