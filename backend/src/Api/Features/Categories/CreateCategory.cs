using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>
/// <c>POST /api/categories</c>: <c>201</c> with the category, <c>Location</c> and <c>ETag</c>. A subcategory needs a
/// top-level, active parent of the same kind. A name already used at the same place (same kind and parent), archived
/// categories included, gives <c>409</c> <c>category.name_taken</c>.
/// </summary>
public static class CreateCategory
{
    /// <param name="Kind">Required for subcategories too: it must be the kind of the parent.</param>
    /// <param name="ParentId">A top-level category of the user, or null for a new top-level category.</param>
    public sealed record Request(
        [Required, MaxLength(Category.NameMaxLength)] string Name,
        [Required] CategoryKind? Kind,
        Guid? ParentId);

    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("", Handle).WithName(nameof(CreateCategory));
    }

    internal static async Task<Created<CategoryResponse>> Handle(
        Request request, AppDbContext db, ICurrentUser currentUser, HttpResponse response, CancellationToken cancellationToken)
    {
        var parent = request.ParentId is { } parentId
            ? await db.Categories.GetReferenceAsync(parentId, "category", "parentId", cancellationToken)
            : null;

        var category = new Category(currentUser.RequiredUserId(), request.Name, request.Kind!.Value, parent);
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        var created = await db.FindCategoryResponseAsync(category.Id, cancellationToken)
            ?? throw new InvalidOperationException("The category just created cannot be read.");

        response.SetVersion(created.Version);
        return TypedResults.Created($"{CategoriesModule.Path}/{category.Id}", created);
    }
}
