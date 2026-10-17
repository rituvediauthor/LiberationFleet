using System.Text.Json;
using LiberationFleet.Server.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LiberationFleet.Server.Tests.Api.Exceptions;

public class UnhandledExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_InDevelopment_ReturnsExceptionMessage()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        var handler = new UnhandledExceptionHandler(NullLogger<UnhandledExceptionHandler>.Instance, environment.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/proposals";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("duplicate envelope key"),
            CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("message").GetString().Should().Be("duplicate envelope key");
    }

    [Fact]
    public async Task TryHandleAsync_InProduction_HidesExceptionMessage()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var handler = new UnhandledExceptionHandler(NullLogger<UnhandledExceptionHandler>.Instance, environment.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("secret internals"),
            CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("message").GetString().Should().Be("An unexpected error occurred.");
    }
}
