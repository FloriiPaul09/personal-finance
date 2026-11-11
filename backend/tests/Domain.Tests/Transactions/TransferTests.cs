using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Transactions;

public class TransferTests
{
    [Fact]
    public void Transfer_MovesAmountBetweenAccounts()
    {
        var checking = AnAccount(name: "Conto corrente");
        var savings = AnAccount(name: "Risparmi");

        var transfer = new Transfer(checking, savings, Today, 250m, "Accantonamento");

        Assert.Equal(2, transfer.Legs.Count);
        Assert.Equal(checking.Id, transfer.Outgoing.AccountId);
        Assert.Equal(-250m, transfer.Outgoing.Amount);
        Assert.Equal(savings.Id, transfer.Incoming.AccountId);
        Assert.Equal(250m, transfer.Incoming.Amount);
        Assert.Equal(0m, transfer.Legs.Sum(leg => leg.Amount));
    }

    [Fact]
    public void Transfer_LegsShareDateDescriptionAndTransfer()
    {
        var transfer = new Transfer(AnAccount(), AnAccount(name: "Risparmi"), Today, 250m, "Accantonamento");

        Assert.All(transfer.Legs, leg =>
        {
            Assert.Equal(TransactionKind.Transfer, leg.Kind);
            Assert.Equal(transfer.Id, leg.TransferId);
            Assert.Null(leg.CategoryId);
            Assert.Equal(Today, leg.Date);
            Assert.Equal("Accantonamento", leg.Description);
            Assert.Equal(transfer.UserId, leg.UserId);
        });
        Assert.Equal(250m, transfer.Amount);
        Assert.Equal(Today, transfer.Date);
    }

    [Fact]
    public void Transfer_RejectsTheSameAccount()
    {
        var account = AnAccount();

        Rule.Violated(DomainErrors.TransferSameAccount, () => new Transfer(account, account, Today, 10m));
    }

    [Fact]
    public void Transfer_RejectsDifferentCurrencies() =>
        Rule.Violated(DomainErrors.TransferCurrencyMismatch, () => new Transfer(AnAccount(), AnAccount(name: "Dollari", currency: "USD"), Today, 10m));

    [Fact]
    public void Transfer_RejectsAnAccountOfAnotherUser() =>
        Rule.Violated(DomainErrors.DifferentUser, () => new Transfer(AnAccount(UserA), AnAccount(UserB), Today, 10m));

    [Fact]
    public void Transfer_RejectsAnArchivedAccount()
    {
        var archived = AnAccount(name: "Vecchio conto");
        archived.Archive();

        Rule.Violated(DomainErrors.AccountArchived, () => new Transfer(AnAccount(), archived, Today, 10m));
    }

    [Theory]
    [InlineData("0", DomainErrors.AmountNotPositive)]
    [InlineData("-10", DomainErrors.AmountNotPositive)]
    [InlineData("10.001", DomainErrors.AmountTooManyDecimals)]
    public void Transfer_RejectsInvalidAmounts(string amount, string code) =>
        Rule.Violated(code, () => new Transfer(AnAccount(), AnAccount(name: "Risparmi"), Today, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Change_UpdatesBothLegsTogether()
    {
        var checking = AnAccount(name: "Conto corrente");
        var savings = AnAccount(name: "Risparmi");
        var card = AnAccount(name: "Carta");
        var transfer = new Transfer(checking, savings, Today, 250m);

        transfer.Change(card, savings, Today.AddDays(1), 300m, "Ricarica");

        Assert.Equal(card.Id, transfer.Outgoing.AccountId);
        Assert.Equal(-300m, transfer.Outgoing.Amount);
        Assert.Equal(savings.Id, transfer.Incoming.AccountId);
        Assert.Equal(300m, transfer.Incoming.Amount);
        Assert.All(transfer.Legs, leg => Assert.Equal(Today.AddDays(1), leg.Date));
        Assert.All(transfer.Legs, leg => Assert.Equal("Ricarica", leg.Description));
    }

    [Fact]
    public void Change_CanSwapTheDirection()
    {
        var checking = AnAccount(name: "Conto corrente");
        var savings = AnAccount(name: "Risparmi");
        var transfer = new Transfer(checking, savings, Today, 100m);

        transfer.Change(savings, checking, Today, 100m, null);

        Assert.Equal(savings.Id, transfer.Outgoing.AccountId);
        Assert.Equal(checking.Id, transfer.Incoming.AccountId);
    }

    [Fact]
    public void Change_ToTheSameAccount_IsRejected()
    {
        var checking = AnAccount();
        var transfer = new Transfer(checking, AnAccount(name: "Risparmi"), Today, 100m);

        Rule.Violated(DomainErrors.TransferSameAccount, () => transfer.Change(checking, checking, Today, 100m, null));
    }

    [Fact]
    public void Change_ToANewArchivedAccount_IsRejected_ButKeepingAnArchivedOneIsAllowed()
    {
        var checking = AnAccount();
        var savings = AnAccount(name: "Risparmi");
        var transfer = new Transfer(checking, savings, Today, 100m);
        var archived = AnAccount(name: "Vecchio conto");
        archived.Archive();
        savings.Archive();

        Rule.Violated(DomainErrors.AccountArchived, () => transfer.Change(checking, archived, Today, 100m, null));
        transfer.Change(checking, savings, Today, 120m, null);

        Assert.Equal(120m, transfer.Amount);
    }
}
