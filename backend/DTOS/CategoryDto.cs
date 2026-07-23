namespace BdServices.Api.DTOs;


public record CategoryDto(
    string Name,
    string Slug,
    string? Description
);