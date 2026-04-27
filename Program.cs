using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaybillApp.Data;
using WaybillApp.Models;
using WaybillApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── БД + Identity ────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")
                  ?? "Data Source=waybill.db"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.Password.RequireDigit          = false;
    opt.Password.RequireUppercase      = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequiredLength        = 4;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath       = "/Account/Login";
    opt.AccessDeniedPath = "/Account/AccessDenied";
    opt.SlidingExpiration = true;
    opt.ExpireTimeSpan  = TimeSpan.FromDays(7);
});

// ─── Сервисы приложения ───────────────────────────────────────────────────────
// Scoped: один экземпляр на HTTP-запрос, имеет доступ к DbContext
builder.Services.AddScoped<WaybillService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ─── Инициализация БД и seed-данные ──────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleMgr     = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr     = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Применяем миграции (создаёт БД если нет)
    db.Database.Migrate();

    // Роли
    foreach (var role in new[] { "admin", "driver", "operator" })
        if (!await roleMgr.RoleExistsAsync(role))
            await roleMgr.CreateAsync(new IdentityRole(role));

    // Администратор по умолчанию
    if (await userMgr.FindByNameAsync("admin") == null)
    {
        var admin = new ApplicationUser
        {
            UserName      = "admin",
            Email         = "admin@waybill.local",
            FullName      = "Администратор",
            Role          = "admin",
            EmailConfirmed = true,
        };
        var result = await userMgr.CreateAsync(admin, "admin123");
        if (result.Succeeded)
            await userMgr.AddToRoleAsync(admin, "admin");
    }
}

// ─── Middleware ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
