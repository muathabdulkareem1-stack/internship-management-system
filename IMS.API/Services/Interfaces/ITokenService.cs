using IMS.API.Models;

namespace IMS.API.Services.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}
