namespace RentEase.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<BookingRequest> Bookings { get; set; } = new List<BookingRequest>();
}