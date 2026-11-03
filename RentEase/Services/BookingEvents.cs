namespace RentEase.Services;

public record BookingCreatedEvent(
    Guid BookingId,
    Guid PropertyId,
    Guid ClientId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalPrice,
    DateTime CreatedAt
);