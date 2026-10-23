using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RentEase.Data;
using RentEase.Models;

namespace RentEase.Controllers;

public class PropertiesViewController : Controller
{
    private readonly RentEaseDbContext _context;
    private readonly IDistributedCache _cache;
    private const string PropertiesCacheKey = "properties_all";

    public PropertiesViewController(RentEaseDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var properties = await _context.Properties
            .AsNoTracking()
            .Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(properties);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Property property, string imageUrl)
    {
        ModelState.Remove(nameof(property.Owner));
        ModelState.Remove(nameof(property.OwnerId));
        ModelState.Remove(nameof(property.Images));
        ModelState.Remove(nameof(property.Amenities));
        ModelState.Remove(nameof(property.TenantId));

        if (!ModelState.IsValid)
        {
            return View(property);
        }

        var owner = await _context.Users.FirstOrDefaultAsync();
        if (owner == null)
        {
            ModelState.AddModelError("", "У базі відсутній користувач-власник.");
            return View(property);
        }

        var tenant = await _context.Tenants.FirstOrDefaultAsync();

        property.Id = Guid.NewGuid();
        property.OwnerId = owner.Id;
        property.TenantId = tenant!.Id;
        property.CreatedAt = DateTime.UtcNow;
        property.Images = new List<PropertyImage>();

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            property.Images.Add(new PropertyImage
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                FileUrl = imageUrl,
                FileName = "photo.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = 1024,
                IsPrimary = true
            });
        }

        _context.Properties.Add(property);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(PropertiesCacheKey);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        var property = await _context.Properties.FindAsync(id);
        if (property != null)
        {
            _context.Properties.Remove(property);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync(PropertiesCacheKey);
        }

        return RedirectToAction(nameof(Index));
    }
}