using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Tags;

/// <summary><c>GET /api/tags</c>: every tag of the user by name, for autocompletion. Not paged.</summary>
public static class ListTags
{
    internal sealed class Endpoint : IEndpoint<TagsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", Handle).WithName(nameof(ListTags));
    }

    internal static async Task<Ok<ListResponse<TagResponse>>> Handle(AppDbContext db, CancellationToken cancellationToken)
    {
        var items = await db.Tags
            .OrderBy(tag => tag.NormalizedName)
            .SelectResponses(db)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ListResponse<TagResponse>(items));
    }
}
