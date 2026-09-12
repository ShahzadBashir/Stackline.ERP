namespace Stackline.API.Features.Categories.Dtos;

public sealed record CreateCategoryRequest(
    string Name,
    Guid? ParentCategoryId);

public sealed record UpdateCategoryRequest(
    string Name,
    Guid? ParentCategoryId,
    bool IsActive);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    Guid? ParentCategoryId,
    bool IsActive,
    DateTime CreatedAt);