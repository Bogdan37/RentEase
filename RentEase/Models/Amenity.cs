namespace RentEase.Models;

public class Amenity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? IconName { get; set; }

    public ICollection<Property> Properties { get; set; } = new List<Property>();
}