using Stackline.API.Common;

namespace Stackline.API.Features.Categories;

public static class CategoryErrors
{
    public static readonly Error NotFound = new(
        "category.not_found",
        "The category was not found.",
        ErrorType.NotFound);

    public static readonly Error NameRequired = new(
        "category.name_required",
        "Category name is required.",
        ErrorType.Validation);

    public static readonly Error ParentNotFound = new(
        "category.parent_not_found",
        "The parent category was not found.",
        ErrorType.Validation);

    public static readonly Error CircularReference = new(
        "category.circular_reference",
        "A category cannot be its own parent or a descendant of itself.",
        ErrorType.Validation);

    public static readonly Error InUse = new(
        "category.in_use",
        "The category has child categories or items.",
        ErrorType.Conflict);

    public static readonly Error NameExists = new(
    "category.name_exists",
    "A category with this name already exists under the selected parent.",
    ErrorType.Conflict);
}