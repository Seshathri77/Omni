using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yath.Shared.DTOs;
using Yath.UserService.Models;

namespace Yath.UserService.Services;

public interface IAuthService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    string GenerateJwtToken(User user);
    LoginResponse CreateLoginResponse(User user, string token);
}

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? "Yath";
        var audience = jwtSettings["Audience"] ?? "YathApp";
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "1440"); // 24 hours default

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("username", user.Username),
            new Claim("displayName", user.Profile.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public LoginResponse CreateLoginResponse(User user, string token)
    {
        return new LoginResponse(
            token,
            new UserDto(
                user.UserId,
                user.Username,
                user.Email,
                user.Profile.DisplayName,
                user.Profile.Bio,
                user.Profile.AvatarUrl,
                user.Profile.Location,
                user.Profile.TravelStyles,
                user.SocialGraph.FollowersCount,
                user.SocialGraph.FollowingCount,
                user.CreatedAt
            )
        );
    }
}
