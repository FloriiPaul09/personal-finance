using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Categories;

public class DefaultCategoriesTests
{
    [Fact]
    public void CreateFor_BuildsEveryCategoryOfTheList()
    {
        var categories = DefaultCategories.CreateFor(UserA);

        Assert.Equal(14, categories.Count(c => !c.IsSubcategory));
        Assert.Equal(6, categories.Count(c => c.IsSubcategory));
        Assert.All(categories, c => Assert.Equal(UserA, c.UserId));
        Assert.All(categories, c => Assert.False(c.IsArchived));
    }

    [Fact]
    public void Subcategories_FollowTheirParent()
    {
        var categories = DefaultCategories.CreateFor(UserA);
        var byId = categories.ToDictionary(c => c.Id);

        Assert.All(categories.Where(c => c.IsSubcategory), sub =>
        {
            var parent = byId[sub.ParentId!.Value];
            Assert.False(parent.IsSubcategory);
            Assert.Equal(parent.Kind, sub.Kind);
        });

        var transport = categories.Single(c => c.Name == "Trasporti");
        Assert.Equal(
            new[] { "Carburante", "Mezzi pubblici", "Auto" },
            categories.Where(c => c.ParentId == transport.Id).Select(c => c.Name));
    }

    [Fact]
    public void Names_AreUniquePerKindAndParent_AsTheDatabaseRequires()
    {
        var keys = DefaultCategories.CreateFor(UserA).Select(c => (c.Kind, c.ParentId, c.NormalizedName)).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Other_ExistsForBothKinds_SoQuickEntryAlwaysHasACategory()
    {
        var others = DefaultCategories.CreateFor(UserA).Where(c => c.Name == "Altro" && !c.IsSubcategory).ToList();

        Assert.Equal(new[] { CategoryKind.Income, CategoryKind.Expense }, others.Select(c => c.Kind).Order());
    }

    [Fact]
    public void CreateFor_GivesEveryUserTheirOwnCopies()
    {
        var a = DefaultCategories.CreateFor(UserA);
        var b = DefaultCategories.CreateFor(UserB);

        Assert.Empty(a.Select(c => c.Id).Intersect(b.Select(c => c.Id)));
        Assert.All(b, c => Assert.Equal(UserB, c.UserId));
    }

    [Fact]
    public void CreateFor_RequiresAUser() =>
        Rule.Violated(DomainErrors.UserRequired, () => DefaultCategories.CreateFor(Guid.Empty));
}
