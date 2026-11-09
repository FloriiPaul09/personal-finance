using System.Reflection;

namespace PersonalFinance.Infrastructure;

/// <summary>Stable handle on this assembly, used by tests and by reflection-based registration.</summary>
public static class InfrastructureAssembly
{
    public static Assembly Reference => typeof(InfrastructureAssembly).Assembly;
}
