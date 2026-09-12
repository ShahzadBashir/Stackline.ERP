using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Categories.Dtos;

namespace Stackline.API.Features.Categories;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var categories = await db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryResponse(
                    c.Id,
                    c.Name,
                    c.ParentCategoryId,
                    c.IsActive,
                    c.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(categories);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var category = await db.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            var result = category is null
                ? Result<CategoryResponse>.Failure(CategoryErrors.NotFound)
                : Result<CategoryResponse>.Success(ToResponse(category));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateCategoryRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var validation = await ValidateAsync(
                request.Name, request.ParentCategoryId, null, db, ct);

            if (!validation.IsSuccess)
            {
                return validation.ToHttpResult(_ => Results.NoContent());
            }

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                ParentCategoryId = request.ParentCategoryId
            };

            db.Categories.Add(category);

            var result = await SaveAsync(category, db, ct);

            return result.ToHttpResult(value =>
                Results.Created($"/api/categories/{value.Id}", value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCategoryRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var category = await db.Categories
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null)
            {
                return Result<CategoryResponse>
                    .Failure(CategoryErrors.NotFound)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var validation = await ValidateAsync(
                request.Name, request.ParentCategoryId, id, db, ct);

            if (!validation.IsSuccess)
            {
                return validation.ToHttpResult(_ => Results.NoContent());
            }

            category.Name = request.Name.Trim();
            category.ParentCategoryId = request.ParentCategoryId;
            category.IsActive = request.IsActive;
            category.UpdatedAt = DateTime.UtcNow;

            var result = await SaveAsync(category, db, ct);

            return result.ToHttpResult(value => Results.Ok(value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var category = await db.Categories
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category is null)
            {
                return Result<Guid>
                    .Failure(CategoryErrors.NotFound)
                    .ToHttpResult(_ => Results.NoContent());
            }

            if (await db.Categories.AnyAsync(
                    c => c.ParentCategoryId == id, ct) ||
                await db.Items.AnyAsync(i => i.CategoryId == id, ct))
            {
                return Result<Guid>
                    .Failure(CategoryErrors.InUse)
                    .ToHttpResult(_ => Results.NoContent());
            }

            category.IsDeleted = true;
            category.IsActive = false;
            category.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Result<Guid>
                .Success(id)
                .ToHttpResult(_ => Results.NoContent());
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = Roles.Owner
        });

        return app;
    }

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Name,
        category.ParentCategoryId,
        category.IsActive,
        category.CreatedAt);

    private static async Task<Result<bool>> ValidateAsync(
    string? name,
    Guid? parentCategoryId,
    Guid? categoryId,
    AppDbContext db,
    CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<bool>.Failure(CategoryErrors.NameRequired);

        var trimmedName = name.Trim();

        var nameExists = await db.Database.SqlQuery<int>($"""
        SELECT 1 AS "Value"
        FROM "Categories"
        WHERE NOT "IsDeleted"
          AND "ParentCategoryId" IS NOT DISTINCT FROM {parentCategoryId}::uuid
          AND "Id" IS DISTINCT FROM {categoryId}::uuid
          AND lower(btrim("Name")) = lower(btrim({trimmedName}))
        """)
            .AnyAsync(ct);

        if (nameExists)
            return Result<bool>.Failure(CategoryErrors.NameExists);

        var visited = new HashSet<Guid>();
        var currentParentId = parentCategoryId;

        while (currentParentId is Guid parentId)
        {
            if (parentId == categoryId || !visited.Add(parentId))
            {
                return Result<bool>.Failure(
                    CategoryErrors.CircularReference);
            }

            var parent = await db.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentId, ct);

            if (parent is null)
                return Result<bool>.Failure(CategoryErrors.ParentNotFound);

            currentParentId = parent.ParentCategoryId;
        }

        return Result<bool>.Success(true);
    }

    private static async Task<Result<CategoryResponse>> SaveAsync(
    Category category,
    AppDbContext db,
    CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);

            return Result<CategoryResponse>.Success(ToResponse(category));
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName:
                    "UX_Categories_Root_Name" or
                    "UX_Categories_Parent_Name"
            })
        {
            return Result<CategoryResponse>.Failure(
                CategoryErrors.NameExists);
        }
    }
}