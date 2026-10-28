using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RentEase.Data;
using RentEase.DTOs;
using RentEase.Models;

namespace RentEase.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly RentEaseDbContext _context;
    private readonly IDistributedCache _cache;
    private const string PropertiesCacheKey = "properties_all";

    public PropertiesController(RentEaseDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PropertyResponseDto>>> GetAll(CancellationToken ct)
    {
        var cachedData = await _cache.GetStringAsync(PropertiesCacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cachedProperties = JsonSerializer.Deserialize<List<PropertyResponseDto>>(cachedData);
            return Ok(cachedProperties);
        }

        var properties = await _context.Properties
            .AsNoTracking()
            .Include(p => p.Images)
            .Select(p => new PropertyResponseDto(
                p.Id,
                p.OwnerId,
                p.Title,
                p.Description,
                p.Type,
                p.PricePerMonth,
                p.AreaSqM,
                p.RoomsCount,
                p.Latitude,
                p.Longitude,
                p.Address,
                p.City,
                p.CreatedAt,
                p.Images.Select(img => new PropertyImageDto(img.Id, img.FileUrl, img.FileName, img.FileSizeBytes, img.IsPrimary)).ToList()
            ))
            .ToListAsync(ct);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        var serializedData = JsonSerializer.Serialize(properties);
        await _cache.SetStringAsync(PropertiesCacheKey, serializedData, options, ct);

        return Ok(properties);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var property = await _context.Properties
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => p.Id == id)
            .Select(p => new PropertyResponseDto(
                p.Id,
                p.OwnerId,
                p.Title,
                p.Description,
                p.Type,
                p.PricePerMonth,
                p.AreaSqM,
                p.RoomsCount,
                p.Latitude,
                p.Longitude,
                p.Address,
                p.City,
                p.CreatedAt,
                p.Images.Select(img => new PropertyImageDto(img.Id, img.FileUrl, img.FileName, img.FileSizeBytes, img.IsPrimary)).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        if (property == null)
            return NotFound();

        return Ok(property);
    }

    [HttpPost]
    public async Task<ActionResult<PropertyResponseDto>> Create([FromBody] CreatePropertyDto dto, CancellationToken ct)
    {
        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.OwnerId, ct);
        if (owner == null)
        {
            return BadRequest($"Користувача з ID {dto.OwnerId} не знайдено.");
        }

        var property = new Property
        {
            TenantId = owner.TenantId, 
            OwnerId = dto.OwnerId,
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            PricePerMonth = dto.PricePerMonth,
            AreaSqM = dto.AreaSqM,
            RoomsCount = dto.RoomsCount,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Address = dto.Address,
            City = dto.City,
            Images = dto.Images?.Select(img => new PropertyImage
            {
                FileUrl = img.FileUrl,
                FileName = img.FileName,
                ContentType = img.ContentType,
                FileSizeBytes = img.FileSizeBytes,
                IsPrimary = img.IsPrimary
            }).ToList() ?? new List<PropertyImage>()
        };

        _context.Properties.Add(property);
        await _context.SaveChangesAsync(ct);

        await _cache.RemoveAsync(PropertiesCacheKey, ct);

        return CreatedAtAction(nameof(GetById), new { id = property.Id }, new PropertyResponseDto(
            property.Id,
            property.OwnerId,
            property.Title,
            property.Description,
            property.Type,
            property.PricePerMonth,
            property.AreaSqM,
            property.RoomsCount,
            property.Latitude,
            property.Longitude,
            property.Address,
            property.City,
            property.CreatedAt,
            property.Images.Select(img => new PropertyImageDto(img.Id, img.FileUrl, img.FileName, img.FileSizeBytes, img.IsPrimary)).ToList()
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePropertyDto dto, CancellationToken ct)
    {
        var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (property == null)
            return NotFound();

        property.Title = dto.Title;
        property.Description = dto.Description;
        property.PricePerMonth = dto.PricePerMonth;
        property.AreaSqM = dto.AreaSqM;
        property.RoomsCount = dto.RoomsCount;
        property.Address = dto.Address;
        property.City = dto.City;

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveAsync(PropertiesCacheKey, ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (property == null)
            return NotFound();

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync(ct);

        await _cache.RemoveAsync(PropertiesCacheKey, ct);

        return NoContent();
    }
}