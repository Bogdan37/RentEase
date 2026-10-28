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
            .Include(p => p.Bookings)
                .ThenInclude(b => b.Client)
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

    [HttpGet]
    public async Task<IActionResult> Book(Guid id)
    {
        var property = await _context.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
        {
            return NotFound();
        }

        ViewBag.Property = property;
        ViewBag.Users = await _context.Users.AsNoTracking().ToListAsync();

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Book(Guid id, Guid clientId, DateOnly startDate, DateOnly endDate)
    {
        var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id);
        if (property == null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (startDate >= endDate)
        {
            ModelState.AddModelError(string.Empty, "Дата початку бронювання повинна бути раніше дати завершення.");
        }

        if (startDate < today)
        {
            ModelState.AddModelError(string.Empty, "Не можна забронювати житло на минулі дати.");
        }

        if (property.OwnerId == clientId)
        {
            ModelState.AddModelError(string.Empty, "Власник не може забронювати власне житло.");
        }

        var hasConflict = await _context.BookingRequests.AnyAsync(b =>
            b.PropertyId == id &&
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending) &&
            b.StartDate < endDate &&
            startDate < b.EndDate);

        if (hasConflict)
        {
            ModelState.AddModelError(string.Empty, "На вибрані дати це житло вже заброньовано або очікує підтвердження.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Property = property;
            ViewBag.Users = await _context.Users.AsNoTracking().ToListAsync();
            return View();
        }

        var totalDays = endDate.DayNumber - startDate.DayNumber;
        var dailyRate = property.PricePerMonth / 30m;
        var calculatedPrice = Math.Round(dailyRate * (totalDays == 0 ? 1 : totalDays), 2);

        var booking = new BookingRequest
        {
            TenantId = property.TenantId,
            PropertyId = property.Id,
            ClientId = clientId,
            StartDate = startDate,
            EndDate = endDate,
            TotalPrice = calculatedPrice,
            Status = BookingStatus.Pending
        };

        _context.BookingRequests.Add(booking);
        await _context.SaveChangesAsync();

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