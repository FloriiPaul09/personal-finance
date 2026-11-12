using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Identity;

/// <summary>
/// Creates the first user when the database has none (ADR-0001): secure by default, an instance never runs without an
/// owner and never with an open registration it did not ask for.
/// </summary>
public sealed class InitialUserInitializer(UserManager<ApplicationUser> userManager, DefaultCategoriesInitializer defaultCategories)
{
    /// <returns>Whether the user was created. Once any user exists, the values are ignored.</returns>
    /// <exception cref="InvalidOperationException">No users and no values, or values Identity refuses.</exception>
    public async Task<bool> EnsureAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "The database has no users. Set Auth__InitialUser__Email and Auth__InitialUser__Password to create the " +
                "first one (docs/devops/configuration.md).");
        }

        var user = new ApplicationUser { UserName = email.Trim(), Email = email.Trim() };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "The initial user could not be created from Auth__InitialUser__*: " +
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        // Outside the transaction that created the user, idempotent (docs/database/default-data.md).
        await defaultCategories.EnsureForUserAsync(user.Id, cancellationToken);
        return true;
    }
}
