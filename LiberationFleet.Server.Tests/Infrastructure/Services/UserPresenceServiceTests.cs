using FluentAssertions;
using LiberationFleet.Server.Infrastructure.Services;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiberationFleet.Server.Tests.Infrastructure.Services;

public class UserPresenceServiceTests
{
    [Fact]
    public async Task RecordActivity_UpdatesStaleLastLoginAt()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var user = context.Users.Single();
        user.LastLoginAt = DateTime.UtcNow.AddHours(-13);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var presence = new UserPresenceService(scopeFactory, NullLogger<UserPresenceService>.Instance);
        presence.RecordActivity(user.Id);

        await WaitForAsync(async () =>
        {
            await context.Entry(user).ReloadAsync();
            return user.LastLoginAt.HasValue
                && user.LastLoginAt.Value > DateTime.UtcNow.AddMinutes(-1);
        });

        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RecordActivity_DoesNotOverwriteFreshLastLoginAt()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var user = context.Users.Single();
        var fresh = DateTime.UtcNow.AddSeconds(-30);
        user.LastLoginAt = fresh;
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var presence = new UserPresenceService(scopeFactory, NullLogger<UserPresenceService>.Instance);
        presence.RecordActivity(user.Id);

        await Task.Delay(150);
        await context.Entry(user).ReloadAsync();
        user.LastLoginAt.Should().Be(fresh);
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met in time.");
    }
}
