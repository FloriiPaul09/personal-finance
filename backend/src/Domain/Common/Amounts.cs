namespace PersonalFinance.Domain.Common;

/// <summary>
/// Rules for amounts stored as <c>numeric(18,2)</c>: at most two decimals, never rounded silently
/// (docs/database/domain-model.md#amounts).
/// </summary>
public static class Amounts
{
    /// <summary>Largest absolute value of <c>numeric(18,2)</c>.</summary>
    public const decimal MaxAbsoluteValue = 9_999_999_999_999_999.99m;

    /// <summary>A valid amount of any sign (e.g. an initial balance).</summary>
    public static decimal EnsureValid(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException(DomainErrors.AmountTooManyDecimals, "An amount has at most two decimals.");
        }

        if (Math.Abs(amount) > MaxAbsoluteValue)
        {
            throw new DomainException(DomainErrors.AmountOutOfRange, "The amount is too large.");
        }

        return amount;
    }

    /// <summary>A valid amount greater than zero (movements and budgets: the domain applies the sign).</summary>
    public static decimal EnsurePositive(decimal amount)
    {
        EnsureValid(amount);

        if (amount <= 0)
        {
            throw new DomainException(DomainErrors.AmountNotPositive, "The amount must be greater than zero.");
        }

        return amount;
    }
}
