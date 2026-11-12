using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentEase.Data;
using RentEase.DTOs;
using RentEase.Models;
using RentEase.Services;

namespace RentEase.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RentEaseDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        RentEaseDbContext context, 
        ITokenService tokenService, 
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> GoogleLogin([FromBody] GoogleAuthDto dto)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Невалідний Google токен: {Message}", ex.Message);
            return BadRequest("Невалідний Google ID Token");
        }

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (user == null)
        {
            var defaultTenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync();

            if (defaultTenant == null)
            {
                defaultTenant = new Tenant { Id = Guid.NewGuid(), Name = "Default Tenant", Slug = "default" };
                _context.Tenants.Add(defaultTenant);
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                Email = payload.Email,
                FullName = payload.Name ?? "Google User",
                PhoneNumber = "+380000000000",
                TenantId = defaultTenant.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        var token = _tokenService.GenerateJwtToken(user);
        return Ok(new AuthResponseDto(
            token, 
            user.Id, 
            user.Email, 
            user.FullName, 
            DateTime.UtcNow.AddMinutes(120)));
    }

    [HttpPost("demo-login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResponseDto>> DemoLogin([FromBody] LoginDemoDto dto)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
        {
            return NotFound($"Користувача з email '{dto.Email}' не знайдено.");
        }

        var token = _tokenService.GenerateJwtToken(user);
        return Ok(new AuthResponseDto(
            token, 
            user.Id, 
            user.Email, 
            user.FullName, 
            DateTime.UtcNow.AddMinutes(120)));
    }
}