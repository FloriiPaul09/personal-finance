namespace PersonalFinance.Api.Common.Responses;

/// <summary>
/// Body of a paged list: the same <c>items</c> envelope as <see cref="ListResponse{T}"/> plus the cursor of the next
/// page, <see langword="null"/> on the last one (ADR-0017). Clients pass it back unchanged as <c>?cursor=</c>.
/// </summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
