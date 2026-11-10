using System.Reflection;

namespace PersonalFinance.Api.Tests;

// Placeholder that keeps the project runnable until the first endpoint test exists ([Backend]):
// Microsoft Testing Platform fails a run that executes zero tests.
// Delete this file together with the first real endpoint test.
public class ScaffoldTests
{
    [Fact]
    public void ApiAssembly_IsLoaded() =>
        Assert.Equal("PersonalFinance.Api", Assembly.Load("PersonalFinance.Api").GetName().Name);
}
