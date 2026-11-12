using System.Reflection;

namespace PersonalFinance.Api;

/// <summary>Stable handle on this assembly, used by tests and by reflection-based registration.</summary>
public static class ApiAssembly
{
    public static Assembly Reference => typeof(ApiAssembly).Assembly;
}
