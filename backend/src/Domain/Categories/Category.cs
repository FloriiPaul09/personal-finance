using System.Diagnostics.CodeAnalysis;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Categories;

/// <summary>
/// Classification of income or expenses, with at most one level of subcategories
/// (docs/database/domain-model.md#categories).
/// </summary>
public sealed class Category : OwnedEntity
{
    public const int NameMaxLength = 100;

    public Category(Guid userId, string name, CategoryKind kind, Category? parent = null)
        : base(userId)
    {
        EnsureDefined(kind);
        Kind = kind;
        Rename(name);

        if (parent is not null)
        {
            AttachTo(parent);
        }
    }

    /// <summary>Used by EF Core when materializing entities.</summary>
    private Category()
    {
        Name = null!;
        NormalizedName = null!;
    }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public CategoryKind Kind { get; private set; }

    public Guid? ParentId { get; private set; }

    public bool IsSubcategory => ParentId is not null;

    public bool IsArchived { get; private set; }

    [MemberNotNull(nameof(Name), nameof(NormalizedName))]
    public void Rename(string name)
    {
        Name = Texts.EnsureName(name, NameMaxLength);
        NormalizedName = Texts.NormalizeName(Name);
    }

    /// <summary>Makes the category a subcategory of <paramref name="parent"/>, or top level when null.</summary>
    /// <param name="hasSubcategories">Whether other categories have this one as parent (checked by the caller).</param>
    /// <remarks>Budgets add their own rule on moves: <c>BudgetPolicy.EnsureCanMove</c>.</remarks>
    public void MoveUnder(Category? parent, bool hasSubcategories)
    {
        if (parent is null)
        {
            ParentId = null;
            return;
        }

        if (hasSubcategories)
        {
            throw new DomainException(DomainErrors.CategoryHasSubcategories, "A category with subcategories cannot become a subcategory.");
        }

        AttachTo(parent);
    }

    /// <summary>
    /// Name, kind and parent together, as an edit form sends them. The kind changes only while nothing uses the
    /// category and it has no subcategories; a subcategory has the kind of its parent; an archived parent is refused
    /// only when newly chosen, so an archived subcategory can still be renamed.
    /// </summary>
    /// <param name="parent">The new parent, or null for a top-level category.</param>
    /// <param name="hasSubcategories">Whether other categories, archived included, have this one as parent (checked by the caller).</param>
    /// <param name="isInUse">Whether transactions or budgets reference the category (checked by the caller).</param>
    /// <remarks>Budgets add their own rule on moves: call <c>BudgetPolicy.EnsureCanMove</c> first.</remarks>
    public void Change(string name, CategoryKind kind, Category? parent, bool hasSubcategories, bool isInUse)
    {
        var newName = Texts.EnsureName(name, NameMaxLength);
        EnsureDefined(kind);

        if (parent is not null)
        {
            EnsureSameUser(parent);

            if (parent.Id == Id)
            {
                throw new DomainException(DomainErrors.CategoryParentItself, "A category cannot be its own parent.");
            }

            if (parent.IsSubcategory)
            {
                throw new DomainException(DomainErrors.CategoryTooDeep, "Subcategories have one level only.");
            }

            if (hasSubcategories)
            {
                throw new DomainException(DomainErrors.CategoryHasSubcategories, "A category with subcategories cannot become a subcategory.");
            }

            if (parent.Kind != kind)
            {
                throw new DomainException(DomainErrors.CategoryKindMismatch, "A subcategory has the kind of its parent.");
            }

            if (parent.Id != ParentId && parent.IsArchived)
            {
                throw new DomainException(DomainErrors.CategoryArchived, "An archived category cannot receive subcategories.");
            }
        }

        if (kind != Kind && (hasSubcategories || isInUse))
        {
            throw new DomainException(DomainErrors.CategoryKindLocked, "The kind of a category in use cannot change.");
        }

        Name = newName;
        NormalizedName = Texts.NormalizeName(newName);
        Kind = kind;
        ParentId = parent?.Id;
    }

    /// <summary>
    /// The kind of a top-level category without subcategories can change while nothing uses it: afterwards existing
    /// transactions and budgets would no longer match it.
    /// </summary>
    /// <param name="isInUse">Whether transactions or budgets reference the category (checked by the caller).</param>
    public void ChangeKind(CategoryKind kind, bool hasSubcategories, bool isInUse)
    {
        EnsureDefined(kind);
        if (kind == Kind)
        {
            return;
        }

        if (IsSubcategory)
        {
            throw new DomainException(DomainErrors.CategoryKindFollowsParent, "A subcategory has the kind of its parent.");
        }

        if (hasSubcategories || isInUse)
        {
            throw new DomainException(DomainErrors.CategoryKindLocked, "The kind of a category in use cannot change.");
        }

        Kind = kind;
    }

    /// <summary>Archives the category and its subcategories, which must all be passed.</summary>
    public void Archive(IEnumerable<Category> subcategories)
    {
        foreach (var subcategory in subcategories)
        {
            if (subcategory.ParentId != Id)
            {
                throw new DomainException(DomainErrors.CategoryNotASubcategory, "The category is not a subcategory of this one.");
            }

            subcategory.IsArchived = true;
        }

        IsArchived = true;
    }

    /// <summary>
    /// Restores this category only: subcategories archived with it stay archived until restored one by one. A
    /// subcategory cannot be restored under an archived parent.
    /// </summary>
    /// <param name="parentIsArchived">Whether the parent of a subcategory is archived (checked by the caller).</param>
    public void Unarchive(bool parentIsArchived)
    {
        if (IsSubcategory && parentIsArchived)
        {
            throw new DomainException(DomainErrors.CategoryArchived, "Restore the parent category first.");
        }

        IsArchived = false;
    }

    /// <summary>
    /// A category can be deleted only while nothing uses it and it has no subcategories; otherwise it is archived, so
    /// history and budgets never lose their category.
    /// </summary>
    /// <param name="hasSubcategories">Whether other categories, archived included, have this one as parent.</param>
    /// <param name="isInUse">Whether transactions or budgets reference the category.</param>
    public void EnsureCanDelete(bool hasSubcategories, bool isInUse)
    {
        if (hasSubcategories)
        {
            throw new DomainException(DomainErrors.CategoryHasSubcategories, "A category with subcategories cannot be deleted: archive it instead.");
        }

        if (isInUse)
        {
            throw new DomainException(DomainErrors.CategoryInUse, "A category used by transactions or budgets cannot be deleted: archive it instead.");
        }
    }

    private void AttachTo(Category parent)
    {
        EnsureSameUser(parent);

        if (parent.Id == Id)
        {
            throw new DomainException(DomainErrors.CategoryParentItself, "A category cannot be its own parent.");
        }

        if (parent.IsSubcategory)
        {
            throw new DomainException(DomainErrors.CategoryTooDeep, "Subcategories have one level only.");
        }

        if (parent.Kind != Kind)
        {
            throw new DomainException(DomainErrors.CategoryKindMismatch, "A subcategory has the kind of its parent.");
        }

        if (parent.IsArchived)
        {
            throw new DomainException(DomainErrors.CategoryArchived, "An archived category cannot receive subcategories.");
        }

        ParentId = parent.Id;
    }

    private static void EnsureDefined(CategoryKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException(DomainErrors.CategoryKindInvalid, "Unknown category kind.");
        }
    }
}
