namespace PersonalFinance.Api.Common.OpenApi;

/// <summary>
/// Marks a query parameter that holds a month (<c>?month=2026-06</c>): the OpenAPI document describes it with the
/// <c>YearMonth</c> schema, like months in JSON (ADR-0029). The handler still reads it as text with
/// <see cref="Requests.QueryValues.ParseMonth"/>, so a malformed month gives <c>400</c> <c>request.invalid</c> on the parameter.
/// </summary>
/// <example><c>string? month = null</c> → <c>[MonthQuery] string? month = null</c></example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MonthQueryAttribute : Attribute;
