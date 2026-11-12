using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Categories;

public class CategoryTests
{
    [Fact]
    public void Create_Subcategory_ReferencesItsParent()
    {
        var transport = ExpenseCategory(name: "Trasporti");

        var fuel = ExpenseCategory(name: "Carburante", parent: transport);

        Assert.Equal(transport.Id, fuel.ParentId);
        Assert.True(fuel.IsSubcategory);
        Assert.False(transport.IsSubcategory);
    }

    [Fact]
    public void Create_RejectsUnknownKinds() =>
        Rule.Violated(DomainErrors.CategoryKindInvalid, () => new Category(UserA, "Spesa", (CategoryKind)7));

    [Fact]
    public void Create_RejectsASecondLevel()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        var car = ExpenseCategory(name: "Auto", parent: transport);

        Rule.Violated(DomainErrors.CategoryTooDeep, () => ExpenseCategory(name: "Gomme", parent: car));
    }

    [Fact]
    public void Create_RejectsAParentOfTheOtherKind() =>
        Rule.Violated(DomainErrors.CategoryKindMismatch, () => ExpenseCategory(name: "Bonus", parent: IncomeCategory()));

    [Fact]
    public void Create_RejectsAParentOfAnotherUser() =>
        Rule.Violated(DomainErrors.DifferentUser, () => ExpenseCategory(UserA, "Carburante", ExpenseCategory(UserB, "Trasporti")));

    [Fact]
    public void Create_RejectsAnArchivedParent()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        transport.Archive([]);

        Rule.Violated(DomainErrors.CategoryArchived, () => ExpenseCategory(name: "Carburante", parent: transport));
    }

    [Fact]
    public void MoveUnder_ToTopLevel_RemovesTheParent()
    {
        var fuel = ExpenseCategory(name: "Carburante", parent: ExpenseCategory(name: "Trasporti"));

        fuel.MoveUnder(null, hasSubcategories: false);

        Assert.Null(fuel.ParentId);
    }

    [Fact]
    public void MoveUnder_RejectsACategoryWithSubcategories() =>
        Rule.Violated(DomainErrors.CategoryHasSubcategories, () => ExpenseCategory(name: "Casa").MoveUnder(ExpenseCategory(name: "Trasporti"), hasSubcategories: true));

    [Fact]
    public void MoveUnder_RejectsItselfAsParent()
    {
        var home = ExpenseCategory(name: "Casa");

        Rule.Violated(DomainErrors.CategoryParentItself, () => home.MoveUnder(home, hasSubcategories: false));
    }

    [Fact]
    public void ChangeKind_IsAllowedForAnUnusedTopLevelCategory()
    {
        var category = ExpenseCategory(name: "Regali");

        category.ChangeKind(CategoryKind.Income, hasSubcategories: false, isInUse: false);

        Assert.Equal(CategoryKind.Income, category.Kind);
    }

    [Fact]
    public void ChangeKind_IsRejectedWhenInUse() =>
        Rule.Violated(DomainErrors.CategoryKindLocked, () => ExpenseCategory().ChangeKind(CategoryKind.Income, hasSubcategories: false, isInUse: true));

    [Fact]
    public void ChangeKind_IsRejectedWithSubcategories() =>
        Rule.Violated(DomainErrors.CategoryKindLocked, () => ExpenseCategory().ChangeKind(CategoryKind.Income, hasSubcategories: true, isInUse: false));

    [Fact]
    public void ChangeKind_IsRejectedForASubcategory()
    {
        var fuel = ExpenseCategory(name: "Carburante", parent: ExpenseCategory(name: "Trasporti"));

        Rule.Violated(DomainErrors.CategoryKindFollowsParent, () => fuel.ChangeKind(CategoryKind.Income, hasSubcategories: false, isInUse: false));
    }

    [Fact]
    public void ChangeKind_ToTheSameKindIsAlwaysAllowed() =>
        ExpenseCategory().ChangeKind(CategoryKind.Expense, hasSubcategories: true, isInUse: true);

    [Fact]
    public void Archive_ArchivesTheSubcategoriesToo()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        var fuel = ExpenseCategory(name: "Carburante", parent: transport);
        var car = ExpenseCategory(name: "Auto", parent: transport);

        transport.Archive([fuel, car]);

        Assert.True(transport.IsArchived);
        Assert.True(fuel.IsArchived);
        Assert.True(car.IsArchived);
    }

    [Fact]
    public void Archive_RejectsCategoriesThatAreNotSubcategories() =>
        Rule.Violated(DomainErrors.CategoryNotASubcategory, () => ExpenseCategory(name: "Casa").Archive([ExpenseCategory(name: "Svago")]));

    // ---- Change (edit form: name, kind and parent together) ----------------------------------------------------

    [Fact]
    public void Change_RenamesMovesAndChangesKind_WhenUnused()
    {
        var salary = IncomeCategory(name: "Lavoro");
        var category = ExpenseCategory(name: "Bonus");

        category.Change(" Premi ", CategoryKind.Income, salary, hasSubcategories: false, isInUse: false);

        Assert.Equal("Premi", category.Name);
        Assert.Equal("PREMI", category.NormalizedName);
        Assert.Equal(CategoryKind.Income, category.Kind);
        Assert.Equal(salary.Id, category.ParentId);
    }

    [Fact]
    public void Change_ToTopLevel_RemovesTheParent()
    {
        var fuel = ExpenseCategory(name: "Carburante", parent: ExpenseCategory(name: "Trasporti"));

        fuel.Change("Carburante", CategoryKind.Expense, null, hasSubcategories: false, isInUse: true);

        Assert.Null(fuel.ParentId);
    }

    [Fact]
    public void Change_SubcategoryToTopLevelOfTheOtherKind_IsAllowedWhenUnused()
    {
        var fuel = ExpenseCategory(name: "Carburante", parent: ExpenseCategory(name: "Trasporti"));

        fuel.Change("Rimborsi", CategoryKind.Income, null, hasSubcategories: false, isInUse: false);

        Assert.Equal(CategoryKind.Income, fuel.Kind);
        Assert.False(fuel.IsSubcategory);
    }

    [Fact]
    public void Change_KindIsLockedWhenInUse() =>
        Rule.Violated(DomainErrors.CategoryKindLocked, () => ExpenseCategory().Change("Spesa", CategoryKind.Income, null, hasSubcategories: false, isInUse: true));

    [Fact]
    public void Change_KindIsLockedWithSubcategories() =>
        Rule.Violated(DomainErrors.CategoryKindLocked, () => ExpenseCategory().Change("Spesa", CategoryKind.Income, null, hasSubcategories: true, isInUse: false));

    [Fact]
    public void Change_SameKind_IsAllowedWhenInUse()
    {
        var category = ExpenseCategory(name: "Spesa");

        category.Change("Spesa alimentare", CategoryKind.Expense, null, hasSubcategories: true, isInUse: true);

        Assert.Equal("Spesa alimentare", category.Name);
    }

    [Fact]
    public void Change_RejectsAParentOfAnotherKind() =>
        Rule.Violated(DomainErrors.CategoryKindMismatch, () => ExpenseCategory().Change("Spesa", CategoryKind.Expense, IncomeCategory(), hasSubcategories: false, isInUse: false));

    [Fact]
    public void Change_RejectsASubcategoryAsParent()
    {
        var car = ExpenseCategory(name: "Auto", parent: ExpenseCategory(name: "Trasporti"));

        Rule.Violated(DomainErrors.CategoryTooDeep, () => ExpenseCategory().Change("Spesa", CategoryKind.Expense, car, hasSubcategories: false, isInUse: false));
    }

    [Fact]
    public void Change_RejectsItselfAsParent()
    {
        var home = ExpenseCategory(name: "Casa");

        Rule.Violated(DomainErrors.CategoryParentItself, () => home.Change("Casa", CategoryKind.Expense, home, hasSubcategories: false, isInUse: false));
    }

    [Fact]
    public void Change_RejectsMovingACategoryWithSubcategories() =>
        Rule.Violated(DomainErrors.CategoryHasSubcategories, () => ExpenseCategory(name: "Casa").Change("Casa", CategoryKind.Expense, ExpenseCategory(name: "Trasporti"), hasSubcategories: true, isInUse: false));

    [Fact]
    public void Change_RejectsANewArchivedParent()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        transport.Archive([]);

        Rule.Violated(DomainErrors.CategoryArchived, () => ExpenseCategory().Change("Spesa", CategoryKind.Expense, transport, hasSubcategories: false, isInUse: false));
    }

    [Fact]
    public void Change_KeepsTheCurrentArchivedParent()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        var fuel = ExpenseCategory(name: "Carburante", parent: transport);
        transport.Archive([fuel]);

        fuel.Change("Benzina", CategoryKind.Expense, transport, hasSubcategories: false, isInUse: true);

        Assert.Equal("Benzina", fuel.Name);
    }

    [Fact]
    public void Change_RejectsAParentOfAnotherUser() =>
        Rule.Violated(DomainErrors.DifferentUser, () => ExpenseCategory(UserA).Change("Spesa", CategoryKind.Expense, ExpenseCategory(UserB, "Casa"), hasSubcategories: false, isInUse: false));

    [Fact]
    public void Change_RejectsAnEmptyName() =>
        Rule.Violated(DomainErrors.NameRequired, () => ExpenseCategory().Change(" ", CategoryKind.Expense, null, hasSubcategories: false, isInUse: false));

    // ---- Unarchive and delete ----------------------------------------------------------------------------------

    [Fact]
    public void Unarchive_RestoresOnlyTheCategory()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        var fuel = ExpenseCategory(name: "Carburante", parent: transport);
        transport.Archive([fuel]);

        transport.Unarchive(parentIsArchived: false);

        Assert.False(transport.IsArchived);
        Assert.True(fuel.IsArchived);
    }

    [Fact]
    public void Unarchive_RejectsASubcategoryOfAnArchivedParent()
    {
        var transport = ExpenseCategory(name: "Trasporti");
        var fuel = ExpenseCategory(name: "Carburante", parent: transport);
        transport.Archive([fuel]);

        Rule.Violated(DomainErrors.CategoryArchived, () => fuel.Unarchive(parentIsArchived: true));
    }

    [Fact]
    public void EnsureCanDelete_IsAllowedWhenUnusedAndWithoutSubcategories() =>
        ExpenseCategory().EnsureCanDelete(hasSubcategories: false, isInUse: false);

    [Fact]
    public void EnsureCanDelete_IsRejectedWithSubcategories() =>
        Rule.Violated(DomainErrors.CategoryHasSubcategories, () => ExpenseCategory().EnsureCanDelete(hasSubcategories: true, isInUse: false));

    [Fact]
    public void EnsureCanDelete_IsRejectedWhenInUse() =>
        Rule.Violated(DomainErrors.CategoryInUse, () => ExpenseCategory().EnsureCanDelete(hasSubcategories: false, isInUse: true));

    [Fact]
    public void Rename_UpdatesTheNormalizedName()
    {
        var category = ExpenseCategory(name: "Spesa");

        category.Rename("Spesa alimentare");

        Assert.Equal("SPESA ALIMENTARE", category.NormalizedName);
    }
}
