namespace RentEase.Models;

public enum PropertyType
{
    Apartment = 1,
    House = 2,
    Commercial = 3
}

public class Property
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public decimal PricePerMonth { get; set; }
    public double AreaSqM { get; set; }
    public int RoomsCount { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
    public ICollection<BookingRequest> Bookings { get; set; } = new List<BookingRequest>();
}
