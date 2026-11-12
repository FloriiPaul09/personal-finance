using System.Text.Json;
using PersonalFinance.Api.Common.Errors;

namespace PersonalFinance.Api.Common.Requests;

/// <summary>Values of the query string that follow the JSON conventions of the API (ADR-0015).</summary>
public static class QueryValues
{
    /// <summary>
    /// An enumeration written as in JSON (<c>?kind=expense</c>, any case); <see langword="null"/> when absent. Numbers
    /// and unknown names give <c>400</c> <c>request.invalid</c> on <paramref name="field"/>, like an unknown value in a body.
    /// </summary>
    /// <remarks>
    /// Minimal API would bind an enum parameter with <c>Enum.TryParse</c>, which takes numbers and the C# names
    /// (<c>Expense</c>): the query string would follow other rules than the body.
    /// </remarks>
    public static TEnum? ParseEnum<TEnum>(string? value, string field)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        foreach (var member in Enum.GetValues<TEnum>())
        {
            if (string.Equals(NameOf(member), value, StringComparison.OrdinalIgnoreCase))
            {
                return member;
            }
        }

        var allowed = string.Join(", ", Enum.GetValues<TEnum>().Select(member => NameOf(member)));
        throw new ApiProblemException(StatusCodes.Status400BadRequest, ApiErrors.RequestInvalid, $"Allowed values: {allowed}.", field);
    }

    private static string NameOf<TEnum>(TEnum member)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(member.ToString());
}
