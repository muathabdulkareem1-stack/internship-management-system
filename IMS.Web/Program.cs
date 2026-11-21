using IMS.Web.Data;
using IMS.Web.Enums;
using IMS.Web.Models;
using IMS.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddHttpClient();
builder.Services.AddScoped<AIService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// ── Seed one user per role ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var seeds = new[]
    {
        new { FullName = "Admin User",       Email = "admin@ims.com",       Password = "Admin123",       Role = Role.Admin       },
        new { FullName = "Coordinator User", Email = "coordinator@ims.com", Password = "Coord123",       Role = Role.Coordinator },
        new { FullName = "Supervisor User",  Email = "supervisor@ims.com",  Password = "Super123",       Role = Role.Supervisor  },
        new { FullName = "Student User",     Email = "student@ims.com",     Password = "Student123",     Role = Role.Student     },
    };

    foreach (var s in seeds)
    {
        if (!db.Users.Any(u => u.Email == s.Email))
        {
            db.Users.Add(new User
            {
                FullName     = s.FullName,
                Email        = s.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(s.Password),
                Role         = s.Role
            });
        }
    }
    db.SaveChanges();
}
// ────────────────────────────────────────────────────────────────────────────

app.Run();
