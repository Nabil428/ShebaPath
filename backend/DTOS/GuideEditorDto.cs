namespace BdServices.Api.DTOs;


public record GuideEditorDto(

    string Title,

    string Slug,

    string Summary,

    string Content,

    int CategoryId,

    string? FeaturedImage,

    string? Keywords,

    string? MetaDescription,

    bool IsFeatured,

    bool IsPublished

);