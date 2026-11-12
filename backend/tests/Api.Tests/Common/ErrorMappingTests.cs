using System.Reflection;
using Microsoft.AspNetCore.Http;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Api.Tests.Common;

// Tables and parsers behind the error responses (docs/backend/validation-and-errors.md).
public sealed class ErrorMappingTests
{
    private static readonly string[] DomainCodes = typeof(DomainErrors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void EveryDomainCode_HasAStatus_AndNothingElseIsMapped()
    {
        Assert.NotEmpty(DomainCodes);
        Assert.Equal(DomainCodes.Order(StringComparer.Ordinal), DomainErrorStatuses.MappedCodes.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(DomainErrors.AmountNotPositive, StatusCodes.Status400BadRequest)]
    [InlineData(DomainErrors.TransferSameAccount, StatusCodes.Status400BadRequest)]
    [InlineData(DomainErrors.AccountArchived, StatusCodes.Status409Conflict)]
    [InlineData(DomainErrors.BudgetBranchConflict, StatusCodes.Status409Conflict)]
    [InlineData(DomainErrors.DifferentUser, StatusCodes.Status500InternalServerError)]
    [InlineData("unknown.code", StatusCodes.Status500InternalServerError)]
    public void DomainCode_GetsTheStatusOfItsFamily(string code, int status)
    {
        Assert.Equal(status, DomainErrorStatuses.StatusOf(code));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ApiErrors.RequestInvalid)]
    [InlineData(StatusCodes.Status401Unauthorized, ApiErrors.AuthenticationRequired)]
    [InlineData(StatusCodes.Status404NotFound, ApiErrors.NotFound)]
    [InlineData(StatusCodes.Status412PreconditionFailed, ApiErrors.VersionMismatch)]
    [InlineData(StatusCodes.Status428PreconditionRequired, ApiErrors.VersionRequired)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, ApiErrors.ServerError)]
    [InlineData(StatusCodes.Status418ImATeapot, "http.418")]
    public void ResponseWithoutCode_GetsTheCodeOfItsStatus(int status, string code)
    {
        Assert.Equal(code, ApiErrors.ForStatus(status));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ApiErrors.RequestInvalid)]
    [InlineData(StatusCodes.Status413PayloadTooLarge, "http.413")]
    public void UnreadableRequest_KeepsItsStatus_WithoutTheFrameworkMessage(int status, string code)
    {
        var problem = ApiExceptionHandler.ToProblem(new BadHttpRequestException("Failed to read parameter \"Request request\".", status));

        Assert.NotNull(problem);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions[ApiErrorHandling.CodeExtension]);
        Assert.DoesNotContain("parameter", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityTag_IsAStrongQuotedVersion()
    {
        Assert.Equal("\"4294967295\"", EntityTag.Format(uint.MaxValue));
        Assert.True(EntityTag.TryParse("\"4294967295\"", out var version));
        Assert.Equal(uint.MaxValue, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"\"")]
    [InlineData("42")]
    [InlineData("*")]
    [InlineData("W/\"42\"")]
    [InlineData("\"4a\"")]
    [InlineData("\"-1\"")]
    [InlineData("\" 42\"")]
    [InlineData("\"4294967296\"")]
    [InlineData("\"1\", \"2\"")]
    public void EntityTag_RejectsAnythingElse(string? value)
    {
        Assert.False(EntityTag.TryParse(value, out _));
    }
}
