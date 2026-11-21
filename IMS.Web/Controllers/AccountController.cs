using System.Security.Claims;
using IMS.Web.Data;
using IMS.Web.Models;
using IMS.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Web.Controllers;

public class AccountController(AppDbContext db) : Controller
{
    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(vm);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return user.Role.ToString() switch
        {
            "Student"     => RedirectToAction("Dashboard", "Student"),
            "Supervisor"  => RedirectToAction("Dashboard", "Supervisor"),
            "Coordinator" => RedirectToAction("Dashboard", "Coordinator"),
            "Admin"       => RedirectToAction("Dashboard", "Admin"),
            _             => RedirectToAction("Login")
        };
    }

    [HttpGet]
    [HttpPost]
    public IActionResult Register() => RedirectToAction("Login");

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "New password and confirmation do not match.";
            return View();
        }

        if (newPassword.Length < 6)
        {
            TempData["Error"] = "New password must be at least 6 characters.";
            return View();
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user   = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            TempData["Error"] = "Current password is incorrect.";
            return View();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();

        TempData["Success"] = "Password changed successfully.";
        return View();
    }
}
