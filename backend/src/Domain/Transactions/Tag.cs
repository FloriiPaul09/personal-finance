using System.Diagnostics.CodeAnalysis;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Transactions;

/// <summary>A free label, unique per user without regard to case, part of the transactions module.</summary>
public sealed class Tag : OwnedEntity
{
    public const int NameMaxLength = 50;

    public Tag(Guid userId, string name)
        : base(userId)
    {
        Rename(name);
    }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    [MemberNotNull(nameof(Name), nameof(NormalizedName))]
    public void Rename(string name)
    {
        Name = Texts.EnsureName(name, NameMaxLength);
        NormalizedName = Texts.NormalizeName(Name);
    }
}
