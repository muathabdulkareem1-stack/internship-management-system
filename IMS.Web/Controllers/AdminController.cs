using IMS.Web.Data;
using IMS.Web.Enums;
using IMS.Web.Models;
using IMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        ViewBag.Students = await db.Users.CountAsync(u => u.Role == Role.Student);
        ViewBag.Supervisors = await db.Users.CountAsync(u => u.Role == Role.Supervisor);
        ViewBag.Coordinators = await db.Users.CountAsync(u => u.Role == Role.Coordinator);
        ViewBag.Admins = await db.Users.CountAsync(u => u.Role == Role.Admin);
        ViewBag.TotalInternships = await db.Internships.CountAsync();
        return View();
    }

    public async Task<IActionResult> Users(string? role)
    {
        var query = db.Users.AsQueryable();
        if (Enum.TryParse<Role>(role, out var parsed))
            query = query.Where(u => u.Role == parsed);

        ViewBag.CurrentRole = role;
        return View(await query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync());
    }

    [HttpGet]
    public IActionResult CreateUser() => View();

    [HttpPost]
    public async Task<IActionResult> CreateUser(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        if (await db.Users.AnyAsync(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Email already in use.");
            return View(vm);
        }

        db.Users.Add(new User
        {
            FullName = vm.FullName,
            Email = vm.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Role = vm.Role
        });

        await db.SaveChangesAsync();
        TempData["Success"] = "User created successfully.";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        var internships = await db.Internships
            .Include(i => i.Assignments).ThenInclude(a => a.Report)
            .Include(i => i.ActivityReports)
            .Include(i => i.Evaluations).ThenInclude(e => e.EvaluationSkills)
            .Where(i => i.StudentId == id || i.SupervisorId == id)
            .ToListAsync();

        foreach (var internship in internships)
        {
            foreach (var assignment in internship.Assignments)
            {
                if (assignment.Report is not null)
                    db.ActivityReports.Remove(assignment.Report);
            }
            db.Assignments.RemoveRange(internship.Assignments);
            db.ActivityReports.RemoveRange(internship.ActivityReports);
            foreach (var eval in internship.Evaluations)
                db.EvaluationSkills.RemoveRange(eval.EvaluationSkills);
            db.Evaluations.RemoveRange(internship.Evaluations);
        }
        db.Internships.RemoveRange(internships);

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        TempData["Success"] = "User deleted.";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(int id, Role newRole)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.Role = newRole;
        await db.SaveChangesAsync();
        return RedirectToAction("Users");
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> EditUser(int id, string fullName, string email, string? newPassword)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
        {
            TempData["Error"] = "That email is already used by another account.";
            return RedirectToAction("EditUser", new { id });
        }

        user.FullName = fullName;
        user.Email    = email;

        if (!string.IsNullOrWhiteSpace(newPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        await db.SaveChangesAsync();
        TempData["Success"] = "User updated successfully.";
        return RedirectToAction("Users");
    }
}
