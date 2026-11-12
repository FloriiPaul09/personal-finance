namespace PersonalFinance.Api.Common.Responses;

/// <summary>
/// Body of every list endpoint: <c>{ "items": [...] }</c> (ADR-0015). Paging, totals or a cursor are added next to
/// <see cref="Items"/> when a list needs them, without breaking existing clients.
/// </summary>
public sealed record ListResponse<T>(IReadOnlyList<T> Items);
