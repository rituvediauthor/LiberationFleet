using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using LiberationFleet.Server.Api.Exceptions;
using Microsoft.AspNetCore.Http;

namespace LiberationFleet.Server.Tests.Api.Exceptions;

public class ValidationExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsNotValidationException_ReturnsFalse()
    {
        var handler = new ValidationExceptionHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException(), CancellationToken.None);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WhenValidationExceptionThrown_ReturnsStructuredBadRequest()
    {
        var handler = new ValidationExceptionHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var validationException = new ValidationException(new[]
        {
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("Email", "A valid email address is required"),
            new ValidationFailure("Password", "Password is required")
        });

        var handled = await handler.TryHandleAsync(context, validationException, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("message").GetString().Should().Be("Validation failed");

        var errors = document.RootElement.GetProperty("errors");
        errors.GetProperty("Email").EnumerateArray().Should().HaveCount(2);
        errors.GetProperty("Password").EnumerateArray().Should().HaveCount(1);
    }
}
