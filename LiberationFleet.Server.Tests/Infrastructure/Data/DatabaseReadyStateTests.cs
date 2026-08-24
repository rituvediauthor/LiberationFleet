using LiberationFleet.Server.Infrastructure.Data;
using FluentAssertions;

namespace LiberationFleet.Server.Tests.Infrastructure.Data;

public class DatabaseReadyStateTests
{
    [Fact]
    public void IsReady_DefaultsToFalse_UntilMarked()
    {
        var state = new DatabaseReadyState();
        state.IsReady.Should().BeFalse();

        state.MarkReady();
        state.IsReady.Should().BeTrue();
    }
}
