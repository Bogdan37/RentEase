using RentEase.Models;

namespace RentEase.DTOs;

public record CreateBookingDto(
    Guid PropertyId,
    Guid ClientId,
    DateOnly StartDate,
    DateOnly EndDate
);

public record BookingResponseDto(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string PropertyTitle,
    Guid ClientId,
    string ClientName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt
);