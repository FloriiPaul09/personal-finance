namespace PersonalFinance.Domain.Categories;

/// <summary>
/// Categories every new user receives (docs/database/default-data.md). Names are in Italian, like the user interface;
/// once created they are ordinary user data.
/// </summary>
public static class DefaultCategories
{
    public static IReadOnlyList<DefaultCategory> All { get; } =
    [
        new("Casa", CategoryKind.Expense, ["Affitto o mutuo", "Bollette", "Manutenzione"]),
        new("Spesa", CategoryKind.Expense),
        new("Trasporti", CategoryKind.Expense, ["Carburante", "Mezzi pubblici", "Auto"]),
        new("Ristoranti e bar", CategoryKind.Expense),
        new("Salute", CategoryKind.Expense),
        new("Svago", CategoryKind.Expense),
        new("Abbigliamento", CategoryKind.Expense),
        new("Abbonamenti", CategoryKind.Expense),
        new("Regali", CategoryKind.Expense),
        new("Altro", CategoryKind.Expense),
        new("Stipendio", CategoryKind.Income),
        new("Rimborsi", CategoryKind.Income),
        new("Regali ricevuti", CategoryKind.Income),
        new("Altro", CategoryKind.Income),
    ];

    /// <summary>The default categories of <paramref name="userId"/>, parents before their subcategories.</summary>
    public static IReadOnlyList<Category> CreateFor(Guid userId)
    {
        var categories = new List<Category>();

        foreach (var definition in All)
        {
            var parent = new Category(userId, definition.Name, definition.Kind);
            categories.Add(parent);
            categories.AddRange(definition.Subcategories.Select(name => new Category(userId, name, definition.Kind, parent)));
        }

        return categories;
    }
}

public sealed record DefaultCategory(string Name, CategoryKind Kind, IReadOnlyList<string> Subcategories)
{
    public DefaultCategory(string name, CategoryKind kind)
        : this(name, kind, [])
    {
    }
}
