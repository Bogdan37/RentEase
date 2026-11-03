namespace RentEase.DTOs;

public record GoogleAuthDto(string IdToken);

public record AuthResponseDto(
    string Token,
    Guid UserId,
    string Email,
    string FullName,
    DateTime ExpiresAt
);

public record LoginDemoDto(string Email);