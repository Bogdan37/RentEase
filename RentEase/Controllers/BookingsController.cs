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
public class BookingsController : ControllerBase
{
    private readonly RentEaseDbContext _context;
    private readonly IMessageProducer _messageProducer;

    public BookingsController(RentEaseDbContext context, IMessageProducer messageProducer)
    {
        _context = context;
        _messageProducer = messageProducer;
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
}