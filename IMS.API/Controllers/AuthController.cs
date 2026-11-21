using IMS.API.Data;
using IMS.API.DTOs.Auth;
using IMS.API.Models;
using IMS.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
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

        return Ok(new AuthResponseDto
        {
            Token = tokenService.GenerateToken(user),
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString()
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        return Ok(new AuthResponseDto
        {
            Token = tokenService.GenerateToken(user),
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString()
        });
    }
}
