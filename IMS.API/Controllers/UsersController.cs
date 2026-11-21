using IMS.API.Data;
using IMS.API.DTOs.Auth;
using IMS.API.Enums;
using IMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(AppDbContext db) : ControllerBase
{
    // Get all users (optionally filter by role)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Role? role)
    {
        var query = db.Users.AsQueryable();
        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        var users = await query
            .Select(u => new { u.Id, u.FullName, u.Email, u.Role, u.CreatedAt })
            .ToListAsync();

        return Ok(users);
    }

    // Get single user
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await db.Users
            .Select(u => new { u.Id, u.FullName, u.Email, u.Role, u.CreatedAt })
            .FirstOrDefaultAsync(u => u.Id == id);

        return user is null ? NotFound() : Ok(user);
    }

    // Create any user (Admin creates accounts for staff)
    [HttpPost]
    public async Task<IActionResult> Create(RegisterDto dto)
    {
        if (await db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email already in use.");

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            new { user.Id, user.FullName, user.Email, user.Role });
    }

    // Update user role
    [HttpPatch("{id}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] Role newRole)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.Role = newRole;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Delete user
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
