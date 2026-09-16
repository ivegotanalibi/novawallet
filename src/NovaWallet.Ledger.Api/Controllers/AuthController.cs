using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace NovaWallet.Ledger.Api.Controllers;

/// <summary>
/// Simplified mock auth controller — issues JWT tokens for testing.
/// In production this would be a proper identity provider.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config) => _config = config;

    /// <summary>
    /// POST /api/auth/token — Issue a JWT for a given customer ID.
    /// </summary>
    [HttpPost("token")]
    public IActionResult GetToken([FromBody] TokenRequest request)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", request.CustomerId),
            new Claim("name", request.CustomerName ?? "Test User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expiresInSeconds = 3600
        });
    }
}

public record TokenRequest(string CustomerId, string? CustomerName);
