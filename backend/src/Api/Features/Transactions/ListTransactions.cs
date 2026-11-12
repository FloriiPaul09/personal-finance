using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Requests;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>
/// <c>GET /api/transactions</c>: newest first (date, then id), paged with a cursor, with optional filters. Transfer legs
/// are included (<c>kind: transfer</c>), so the list of an account explains its whole balance.
/// </summary>
public static class ListTransactions
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
    public const int MaxSearchLength = 100;

    internal sealed class Endpoint : IEndpoint<TransactionsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", Handle).WithName(nameof(ListTransactions));
    }

    /// <param name="from">First date included.</param>
    /// <param name="to">Last date included.</param>
    /// <param name="categoryId">A top-level category includes its subcategories.</param>
    /// <param name="tag">Tag name, any case.</param>
    /// <param name="q">Text contained in the description, any case.</param>
    /// <param name="cursor">The <c>nextCursor</c> of the previous page.</param>
    /// <param name="limit">Items per page, 1–200 (default 50).</param>
    internal static async Task<Ok<PageResponse<TransactionResponse>>> Handle(
        AppDbContext db,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? accountId = null,
        Guid? categoryId = null,
        string? kind = null,
        string? tag = null,
        string? q = null,
        string? cursor = null,
        int? limit = null)
    {
        var pageSize = limit ?? DefaultLimit;
        if (pageSize is < 1 or > MaxLimit)
        {
            throw ApiProblemException.InvalidQuery(nameof(limit), $"The limit is between 1 and {MaxLimit}.");
        }

        if (from > to)
        {
            throw ApiProblemException.InvalidQuery(nameof(to), "The period ends before it starts.");
        }

        if (q?.Length > MaxSearchLength)
        {
            throw ApiProblemException.InvalidQuery(nameof(q), $"The search text has at most {MaxSearchLength} characters.");
        }

        var kindFilter = QueryValues.ParseEnum<TransactionKind>(kind, nameof(kind));
        var position = TransactionCursor.Decode(cursor, nameof(cursor));

        var transactions = db.Transactions.AsQueryable();

        if (from is { } firstDate)
        {
            transactions = transactions.Where(transaction => transaction.Date >= firstDate);
        }

        if (to is { } lastDate)
        {
            transactions = transactions.Where(transaction => transaction.Date <= lastDate);
        }

        if (accountId is { } account)
        {
            transactions = transactions.Where(transaction => transaction.AccountId == account);
        }

        if (categoryId is { } category)
        {
            var branch = db.Categories
                .Where(item => item.Id == category || item.ParentId == category)
                .Select(item => item.Id);
            transactions = transactions.Where(transaction => transaction.CategoryId.HasValue && branch.Contains(transaction.CategoryId.Value));
        }

        if (kindFilter is { } selectedKind)
        {
            transactions = transactions.Where(transaction => transaction.Kind == selectedKind);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagName = Texts.NormalizeName(tag);
            transactions = transactions.Where(transaction =>
                transaction.Tags.Any(link => db.Tags.Any(item => item.Id == link.TagId && item.NormalizedName == tagName)));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLike(q.Trim())}%";
            transactions = transactions.Where(transaction =>
                transaction.Description != null && EF.Functions.ILike(transaction.Description, pattern, @"\"));
        }

        if (position is { } after)
        {
            transactions = transactions.Where(transaction =>
                EF.Functions.LessThan(ValueTuple.Create(transaction.Date, transaction.Id), ValueTuple.Create(after.Date, after.Id)));
        }

        // One more than the page: tells whether another page follows without counting.
        var items = await transactions
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .Take(pageSize + 1)
            .SelectResponses(db)
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            var last = items[^1];
            nextCursor = new TransactionCursor(last.Date, last.Id).Encode();
        }

        return TypedResults.Ok(new PageResponse<TransactionResponse>(items, nextCursor));
    }

    /// <summary>The text matched literally: <c>%</c>, <c>_</c> and the escape character lose their meaning in <c>ILIKE</c>.</summary>
    internal static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
