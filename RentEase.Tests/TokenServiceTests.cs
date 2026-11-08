using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RentEase.Models;
using RentEase.Services;
using Xunit;

namespace RentEase.Tests;

public class TokenServiceTests
{
    [Fact]
    public void GenerateJwtToken_ValidUser_IncludesTenantIdAndRequiredClaims()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "RentEaseSecretSuperSecurityKey2026!LongKeyEnoughForSha256" },
            { "Jwt:Issuer", "RentEaseApi" },
            { "Jwt:Audience", "RentEaseClients" },
            { "Jwt:ExpireMinutes", "60" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var tokenService = new TokenService(configuration);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "bohdan@example.com",
            FullName = "Богдан Мігунов",
            TenantId = Guid.NewGuid()
        };

        var tokenString = tokenService.GenerateJwtToken(user);

        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        jwtToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == "email" && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == "tenantId" && c.Value == user.TenantId.ToString());
    }
}