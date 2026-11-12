using System.Reflection;
using System.Text.RegularExpressions;

namespace PersonalFinance.Api.Common.Endpoints;

/// <summary>A module and the endpoints that belong to it.</summary>
internal sealed record ModuleEndpoints(ApiModule Module, IReadOnlyList<IEndpoint> Endpoints);

/// <summary>
/// Finds modules and endpoints by reflection (ADR-0012). Every mistake that would leave an endpoint unmapped or
/// mapped twice stops the application at startup, with the name of the type to fix.
/// </summary>
internal static partial class EndpointDiscovery
{
    public static IReadOnlyList<ModuleEndpoints> FromAssembly(Assembly assembly) => FromTypes(assembly.GetTypes());

    public static IReadOnlyList<ModuleEndpoints> FromTypes(IEnumerable<Type> types)
    {
        var concreteTypes = types.Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }).ToList();

        var endpointsByModule = concreteTypes
            .Where(type => typeof(IEndpoint).IsAssignableFrom(type))
            .GroupBy(ModuleOf)
            .ToDictionary(group => group.Key, group => group.OrderBy(type => type.FullName, StringComparer.Ordinal).ToList());

        var moduleTypes = concreteTypes
            .Where(type => typeof(ApiModule).IsAssignableFrom(type))
            .Union(endpointsByModule.Keys);

        var modules = moduleTypes
            .Select(moduleType => new ModuleEndpoints(
                Create<ApiModule>(moduleType),
                endpointsByModule.TryGetValue(moduleType, out var endpointTypes)
                    ? endpointTypes.Select(Create<IEndpoint>).ToList()
                    : []))
            .OrderBy(module => module.Module.Prefix, StringComparer.Ordinal)
            .ToList();

        foreach (var module in modules)
        {
            EnsureValid(module.Module);
        }

        var duplicate = modules.GroupBy(module => module.Module.Prefix).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Modules {string.Join(" and ", duplicate.Select(module => module.Module.GetType().FullName))} " +
                $"share the prefix '{duplicate.Key}'. Every module needs its own prefix.");
        }

        return modules;
    }

    private static Type ModuleOf(Type endpointType)
    {
        var modules = endpointType.GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEndpoint<>))
            .Select(type => type.GetGenericArguments()[0])
            .ToList();

        return modules.Count switch
        {
            1 => modules[0],
            0 => throw new InvalidOperationException(
                $"{endpointType.FullName} implements IEndpoint without a module. Implement IEndpoint<TModule> instead."),
            _ => throw new InvalidOperationException(
                $"{endpointType.FullName} belongs to more than one module ({string.Join(", ", modules.Select(module => module.Name))}). " +
                "An endpoint belongs to exactly one module."),
        };
    }

    private static void EnsureValid(ApiModule module)
    {
        if (!PrefixPattern().IsMatch(module.Prefix))
        {
            throw new InvalidOperationException(
                $"{module.GetType().FullName} has prefix '{module.Prefix}'. " +
                "A prefix is lowercase words separated by hyphens, without slashes (e.g. 'accounts').");
        }

        if (string.IsNullOrWhiteSpace(module.Tag))
        {
            throw new InvalidOperationException($"{module.GetType().FullName} has no OpenAPI tag.");
        }
    }

    private static T Create<T>(Type type) =>
        (T)(Activator.CreateInstance(type, nonPublic: true)
            ?? throw new InvalidOperationException($"{type.FullName} could not be created."));

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex PrefixPattern();
}
