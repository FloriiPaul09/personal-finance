using System.Reflection;

namespace PersonalFinance.Domain;

/// <summary>Stable handle on this assembly, used by tests and by reflection-based registration.</summary>
public static class DomainAssembly
{
    public static Assembly Reference => typeof(DomainAssembly).Assembly;
}
