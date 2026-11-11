namespace PersonalFinance.Domain.Common;

/// <summary>
/// A business rule was violated. <see cref="Code"/> is stable and machine-readable (see <see cref="DomainErrors"/>);
/// the API maps it to ProblemDetails (ADR-0010).
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
