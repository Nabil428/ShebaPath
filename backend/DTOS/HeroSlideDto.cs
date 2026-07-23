namespace BdServices.Api.DTOs;


public record HeroSlideDto(
    int GuideId,
    string ImageUrl,
    string Title,
    string? Subtitle,
    string? ButtonText,
    string? ButtonLink,
    int DisplayOrder,
    bool IsActive
);