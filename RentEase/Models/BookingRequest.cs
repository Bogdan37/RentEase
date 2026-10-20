namespace RentEase.Models;

public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4
}

public class BookingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    public Guid ClientId { get; set; }
    public User? Client { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}