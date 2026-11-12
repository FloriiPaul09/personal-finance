using System.ComponentModel.DataAnnotations;

namespace PersonalFinance.Api.Common.Requests;

/// <summary>
/// An optional identifier chosen by the client must be a UUID version 7 (time-ordered, like the ids the server
/// generates): random ids would scatter inserts across the primary key index (ADR-0017).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class UuidV7Attribute : ValidationAttribute
{
    public UuidV7Attribute()
        : base("The {0} field must be a UUID version 7.")
    {
    }

    public override bool IsValid(object? value) => value is null || value is Guid { Version: 7 };
}
