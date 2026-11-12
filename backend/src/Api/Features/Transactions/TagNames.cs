using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>Tags named in a transaction request: existing ones are reused, missing ones created (ADR-0017).</summary>
internal static class TagNames
{
    /// <summary>Most tags one transaction can carry.</summary>
    public const int MaxPerTransaction = 20;

    /// <summary>
    /// The tags of the current user with these names, compared without regard to case. Missing tags are added to
    /// <paramref name="db"/> and saved with the transaction; invalid names fail like any name (<c>name.required</c>,
    /// <c>name.too_long</c>).
    /// </summary>
    /// <remarks>
    /// Two requests creating the same new tag at the same instant: the second gets <c>409</c> <c>tag.name_taken</c>
    /// from the unique index and can be sent again.
    /// </remarks>
    public static async Task<IReadOnlyList<Tag>> ResolveAsync(
        AppDbContext db, Guid userId, IReadOnlyCollection<string>? names, CancellationToken cancellationToken)
    {
        if (names is null || names.Count == 0)
        {
            return [];
        }

        var wanted = names
            .GroupBy(name => Texts.NormalizeName(name ?? string.Empty), StringComparer.Ordinal)
            .Select(group => (Normalized: group.Key, Name: group.First()))
            .ToList();

        var normalizedNames = wanted.Select(tag => tag.Normalized).ToList();
        var existing = await db.Tags
            .Where(tag => normalizedNames.Contains(tag.NormalizedName))
            .ToDictionaryAsync(tag => tag.NormalizedName, StringComparer.Ordinal, cancellationToken);

        var tags = new List<Tag>(wanted.Count);
        foreach (var (normalized, name) in wanted)
        {
            if (!existing.TryGetValue(normalized, out var tag))
            {
                tag = new Tag(userId, name);
                db.Tags.Add(tag);
            }

            tags.Add(tag);
        }

        return tags;
    }
}
