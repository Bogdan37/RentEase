using RentEase.Models;
namespace RentEase.DTOs;

public record PropertyResponseDto(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Description,
    PropertyType Type,
    decimal PricePerMonth,
    double AreaSqM,
    int RoomsCount,
    double Latitude,
    double Longitude,
    string Address,
    string City,
    DateTime CreatedAt,
    List<PropertyImageDto> Images
);

public record PropertyImageDto(
    Guid Id,
    string FileUrl,
    string FileName,
    long FileSizeBytes,
    bool IsPrimary
);

public record CreatePropertyDto(
    Guid OwnerId,
    string Title,
    string Description,
    PropertyType Type,
    decimal PricePerMonth,
    double AreaSqM,
    int RoomsCount,
    double Latitude,
    double Longitude,
    string Address,
    string City,
    List<CreatePropertyImageDto>? Images
);

public record CreatePropertyImageDto(
    string FileUrl,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    bool IsPrimary
);

public record UpdatePropertyDto(
    string Title,
    string Description,
    decimal PricePerMonth,
    double AreaSqM,
    int RoomsCount,
    string Address,
    string City
);