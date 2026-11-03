using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RentEase.Data;
using RentEase.DTOs;
using RentEase.Models;
using RentEase.Services;

namespace RentEase.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PropertiesController : ControllerBase
{
    private readonly RentEaseDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IPropertySearchService _searchService;
    private readonly ILogger<PropertiesController> _logger;
    private const string PropertiesCacheKey = "properties_all";

    public PropertiesController(
        RentEaseDbContext context, 
        IDistributedCache cache, 
        IPropertySearchService searchService,
        ILogger<PropertiesController> logger)
    {
        _context = context;
        _cache = cache;
        _searchService = searchService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PropertyResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PropertyResponseDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var cacheKey = $"properties_page_{page}_size_{pageSize}";

        try
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cachedData))
            {
                var cachedResult = JsonSerializer.Deserialize<PagedResult<PropertyResponseDto>>(cachedData);
                if (cachedResult != null)
                {
                    return Ok(cachedResult);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка читання кешу Redis за ключем {CacheKey}. Запит буде виконано безпосередньо до БД.", cacheKey);
        }

        var totalCount = await _context.Properties.CountAsync(ct);

        var items = await _context.Properties
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                p.Images.Select(img => new PropertyImageDto(
                    img.Id, 
                    img.FileUrl, 
                    img.FileName, 
                    img.FileSizeBytes, 
                    img.IsPrimary)).ToList()
            ))
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PagedResult<PropertyResponseDto>(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages,
            HasPreviousPage: page > 1,
            HasNextPage: page < totalPages
        );

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            var serializedData = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, serializedData, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка запису результатів у кеш Redis за ключем {CacheKey}.", cacheKey);
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PropertyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
                p.Images.Select(img => new PropertyImageDto(
                    img.Id, 
                    img.FileUrl, 
                    img.FileName, 
                    img.FileSizeBytes, 
                    img.IsPrimary)).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        if (property == null)
            return NotFound();

        return Ok(property);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(PropertyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PropertyResponseDto>> Create([FromBody] CreatePropertyDto dto, CancellationToken ct)
    {
        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.OwnerId, ct);
        if (owner == null)
        {
            return BadRequest($"Користувача з ID {dto.OwnerId} не знайдено.");
        }

        var property = new Property
        {
            Id = Guid.NewGuid(),
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
            CreatedAt = DateTime.UtcNow,
            Images = dto.Images?.Select(img => new PropertyImage
            {
                Id = Guid.NewGuid(),
                FileUrl = img.FileUrl,
                FileName = img.FileName,
                ContentType = img.ContentType,
                FileSizeBytes = img.FileSizeBytes,
                IsPrimary = img.IsPrimary,
                UploadedAt = DateTime.UtcNow
            }).ToList() ?? new List<PropertyImage>()
        };

        _context.Properties.Add(property);
        await _context.SaveChangesAsync(ct);
        
        try
        {
            _searchService.IndexProperty(property);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося проіндексувати оголошення з ID {PropertyId} у пошуковому сервісі Lucene.", property.Id);
        }

        try
        {
            await _cache.RemoveAsync(PropertiesCacheKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка інвалідації кешу Redis за ключем {CacheKey}.", PropertiesCacheKey);
        }

        var imageDtos = property.Images?.Select(img => new PropertyImageDto(
            img.Id, 
            img.FileUrl, 
            img.FileName, 
            img.FileSizeBytes, 
            img.IsPrimary)).ToList() ?? new List<PropertyImageDto>();

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
            imageDtos
        ));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        try
        {
            _searchService.IndexProperty(property);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося оновити індекс пошуку Lucene для оголошення з ID {PropertyId}.", id);
        }

        try
        {
            await _cache.RemoveAsync(PropertiesCacheKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка інвалідації кешу Redis за ключем {CacheKey}.", PropertiesCacheKey);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (property == null)
            return NotFound();

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync(ct);
        
        try
        {
            _searchService.RemoveProperty(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося видалити оголошення з ID {PropertyId} з індексу Lucene.", id);
        }

        try
        {
            await _cache.RemoveAsync(PropertiesCacheKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка інвалідації кешу Redis за ключем {CacheKey}.", PropertiesCacheKey);
        }

        return NoContent();
    }
    
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<PropertyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PropertyResponseDto>>> Search(
        [FromQuery] string q, 
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Пошуковий запит не може бути порожнім.");

        var matchedIds = _searchService.Search(q).ToList();
        if (!matchedIds.Any())
            return Ok(Enumerable.Empty<PropertyResponseDto>());

        var properties = await _context.Properties
            .AsNoTracking()
            .Where(p => matchedIds.Contains(p.Id))
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
                p.Images.Select(img => new PropertyImageDto(
                    img.Id, 
                    img.FileUrl, 
                    img.FileName, 
                    img.FileSizeBytes, 
                    img.IsPrimary)).ToList()
            ))
            .ToListAsync(ct);

        return Ok(properties);
    }
}