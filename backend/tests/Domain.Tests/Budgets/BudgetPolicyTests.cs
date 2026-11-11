using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Budgets;

public class BudgetPolicyTests
{
    private readonly Category _transport = ExpenseCategory(name: "Trasporti");
    private readonly Category _fuel;
    private readonly Category _car;

    public BudgetPolicyTests()
    {
        _fuel = ExpenseCategory(name: "Carburante", parent: _transport);
        _car = ExpenseCategory(name: "Auto", parent: _transport);
    }

    [Fact]
    public void Subcategory_CanBeBudgeted_WhenTheParentIsNot() =>
        BudgetPolicy.EnsureCanBudget(_fuel, [_transport, _fuel, _car], new HashSet<Guid> { _car.Id });

    [Fact]
    public void Subcategory_CannotBeBudgeted_WhenTheParentIs() =>
        Rule.Violated(DomainErrors.BudgetBranchConflict, () => BudgetPolicy.EnsureCanBudget(_fuel, [_transport, _fuel, _car], new HashSet<Guid> { _transport.Id }));

    [Fact]
    public void Parent_CannotBeBudgeted_WhenASubcategoryIs() =>
        Rule.Violated(DomainErrors.BudgetBranchConflict, () => BudgetPolicy.EnsureCanBudget(_transport, [_transport, _fuel, _car], new HashSet<Guid> { _car.Id }));

    [Fact]
    public void Parent_CanBeBudgeted_WhenOnlyOtherBranchesAre()
    {
        var home = ExpenseCategory(name: "Casa");
        var rent = ExpenseCategory(name: "Affitto", parent: home);

        BudgetPolicy.EnsureCanBudget(_transport, [_transport, _fuel, _car, home, rent], new HashSet<Guid> { rent.Id, _transport.Id });
    }

    [Fact]
    public void BudgetedCategory_CannotMoveUnderABudgetedParent()
    {
        var parking = ExpenseCategory(name: "Parcheggi");

        Rule.Violated(DomainErrors.BudgetBranchConflict, () => BudgetPolicy.EnsureCanMove(parking, _transport, new HashSet<Guid> { parking.Id, _transport.Id }));
    }

    [Fact]
    public void BudgetedCategory_CanMoveToTopLevel_OrUnderAnUnbudgetedParent()
    {
        BudgetPolicy.EnsureCanMove(_fuel, null, new HashSet<Guid> { _fuel.Id, _transport.Id });
        BudgetPolicy.EnsureCanMove(ExpenseCategory(name: "Parcheggi"), _transport, new HashSet<Guid> { _fuel.Id });
    }
}
