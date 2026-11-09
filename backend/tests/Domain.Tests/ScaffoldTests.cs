using PersonalFinance.Domain;

namespace PersonalFinance.Domain.Tests;

// Placeholder that keeps the project runnable until the first business rule exists:
// Microsoft Testing Platform fails a run that executes zero tests.
// Delete this file together with the first real domain test.
public class ScaffoldTests
{
    [Fact]
    public void DomainAssembly_IsLoaded() =>
        Assert.Equal("PersonalFinance.Domain", DomainAssembly.Reference.GetName().Name);
}
