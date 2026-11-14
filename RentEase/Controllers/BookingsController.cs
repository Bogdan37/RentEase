using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RentEase.Data;
using RentEase.DTOs;
using RentEase.Hubs;
using RentEase.Models;
using RentEase.Services;

namespace RentEase.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly RentEaseDbContext _context;
    private readonly IMessageProducer _messageProducer;
    private readonly IHubContext<BookingHub> _hubContext;

    public BookingsController(
        RentEaseDbContext context,
        IMessageProducer messageProducer,
        IHubContext<BookingHub> hubContext)
    {
        _context = context;
        _messageProducer = messageProducer;
        _hubContext = hubContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> Create(
        [FromBody] CreateBookingDto dto,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (dto.StartDate >= dto.EndDate)
        {
            return BadRequest("Дата початку бронювання повинна бути раніше дати завершення.");
        }

        if (dto.StartDate < today)
        {
            return BadRequest("Не можна забронювати житло на минулі дати.");
        }

        var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == dto.PropertyId, ct);
        if (property == null)
        {
            return NotFound($"Житло з ID {dto.PropertyId} не знайдено.");
        }

        if (property.OwnerId == dto.ClientId)
        {
            return BadRequest("Власник не може забронювати власне житло.");
        }

        var client = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.ClientId, ct);
        if (client == null)
        {
            return NotFound($"Клієнта з ID {dto.ClientId} не знайдено.");
        }

        var hasConflict = await _context.BookingRequests.AnyAsync(b =>
            b.PropertyId == dto.PropertyId &&
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending) &&
            b.StartDate < dto.EndDate &&
            dto.StartDate < b.EndDate,
            ct);

        if (hasConflict)
        {
            return BadRequest("На вибрані дати це житло вже заброньовано іншим користувачем.");
        }

        var totalDays = dto.EndDate.DayNumber - dto.StartDate.DayNumber;
        var dailyRate = property.PricePerMonth / 30m;
        var calculatedPrice = Math.Round(dailyRate * (totalDays == 0 ? 1 : totalDays), 2);

        var booking = new BookingRequest
        {
            TenantId = property.TenantId,
            PropertyId = dto.PropertyId,
            ClientId = dto.ClientId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            TotalPrice = calculatedPrice,
            Status = BookingStatus.Pending
        };

        _context.BookingRequests.Add(booking);
        await _context.SaveChangesAsync(ct);

        var bookingEvent = new BookingCreatedEvent(
            booking.Id,
            booking.PropertyId,
            booking.ClientId,
            booking.StartDate,
            booking.EndDate,
            booking.TotalPrice,
            DateTime.UtcNow
        );
        _messageProducer.SendMessage(bookingEvent, "booking_created_queue");

        var response = new BookingResponseDto(
            booking.Id,
            booking.TenantId,
            booking.PropertyId,
            property.Title,
            booking.ClientId,
            client.FullName,
            booking.StartDate,
            booking.EndDate,
            booking.TotalPrice,
            booking.Status.ToString(),
            booking.CreatedAt
        );

        await _hubContext.Clients
            .Group($"user_{property.OwnerId.ToString().ToLowerInvariant()}")
            .SendAsync("BookingCreated", response, ct);

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BookingResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BookingResponseDto>>> GetAll(CancellationToken ct)
    {
        var bookings = await _context.BookingRequests
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingResponseDto(
                b.Id,
                b.TenantId,
                b.PropertyId,
                b.Property != null ? b.Property.Title : string.Empty,
                b.ClientId,
                b.Client != null ? b.Client.FullName : string.Empty,
                b.StartDate,
                b.EndDate,
                b.TotalPrice,
                b.Status.ToString(),
                b.CreatedAt
            ))
            .ToListAsync(ct);

        return Ok(bookings);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var booking = await _context.BookingRequests
            .Include(b => b.Property)
            .Include(b => b.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (booking == null)
            return NotFound();

        return Ok(new BookingResponseDto(
            booking.Id,
            booking.TenantId,
            booking.PropertyId,
            booking.Property?.Title ?? string.Empty,
            booking.ClientId,
            booking.Client?.FullName ?? string.Empty,
            booking.StartDate,
            booking.EndDate,
            booking.TotalPrice,
            booking.Status.ToString(),
            booking.CreatedAt
        ));
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateBookingStatusDto dto,
        CancellationToken ct)
    {
        if (!Enum.TryParse<BookingStatus>(dto.Status, ignoreCase: true, out var parsedStatus))
        {
            return BadRequest("Некоректний статус. Доступні значення: Pending, Confirmed, Rejected, Cancelled.");
        }

        var booking = await _context.BookingRequests
            .Include(b => b.Property)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (booking == null)
        {
            return NotFound($"Бронювання з ID {id} не знайдено.");
        }

        if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Rejected)
        {
            return BadRequest($"Неможливо змінити статус бронювання, оскільки воно вже має фінальний статус '{booking.Status}'.");
        }

        booking.Status = parsedStatus;
        await _context.SaveChangesAsync(ct);

        var response = new BookingResponseDto(
            booking.Id,
            booking.TenantId,
            booking.PropertyId,
            booking.Property?.Title ?? string.Empty,
            booking.ClientId,
            booking.Client?.FullName ?? string.Empty,
            booking.StartDate,
            booking.EndDate,
            booking.TotalPrice,
            booking.Status.ToString(),
            booking.CreatedAt
        );

        var targetGroups = new List<string>
        {
            $"user_{booking.ClientId.ToString().ToLowerInvariant()}"
        };
        if (booking.Property != null)
        {
            targetGroups.Add($"user_{booking.Property.OwnerId.ToString().ToLowerInvariant()}");
        }

        await _hubContext.Clients
            .Groups(targetGroups.Distinct())
            .SendAsync("BookingStatusUpdated", response, ct);

        return Ok(response);
    }
}