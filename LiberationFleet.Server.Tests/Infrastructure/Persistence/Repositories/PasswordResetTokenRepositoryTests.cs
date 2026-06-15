using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepositoryTests
{
    [Fact]
    public async Task GetActiveByTokenAsync_WhenTokenExistsAndIsActive_ReturnsTokenWithUser()
    {
        var (context, token) = await TestDbContextFactory.CreateWithResetTokenAsync();
        await using (context)
        {
            var repository = new PasswordResetTokenRepository(context);

            var result = await repository.GetActiveByTokenAsync("valid-reset-token");

            result.Should().NotBeNull();
            result!.Token.Should().Be("valid-reset-token");
            result.User.Should().NotBeNull();
            result.User.Email.Should().Be("test@example.com");
        }
    }

    [Fact]
    public async Task GetActiveByTokenAsync_WhenTokenIsUsed_ReturnsNull()
    {
        var (context, _) = await TestDbContextFactory.CreateWithResetTokenAsync(isUsed: true);
        await using (context)
        {
            var repository = new PasswordResetTokenRepository(context);

            var result = await repository.GetActiveByTokenAsync("valid-reset-token");

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetActiveByTokenAsync_WhenTokenDoesNotExist_ReturnsNull()
    {
        await using var context = TestDbContextFactory.Create();
        var repository = new PasswordResetTokenRepository(context);

        var result = await repository.GetActiveByTokenAsync("missing-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_PersistsTokenAfterSaveChanges()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var repository = new PasswordResetTokenRepository(context);
        var user = context.Users.Single();

        var token = new PasswordResetToken
        {
            UserId = user.Id,
            Token = "new-token",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsUsed = false
        };

        await repository.AddAsync(token);
        await context.SaveChangesAsync();

        context.PasswordResetTokens.Should().ContainSingle(t => t.Token == "new-token");
    }

    [Fact]
    public async Task UpdateAsync_PersistsTokenChangesAfterSaveChanges()
    {
        var (context, token) = await TestDbContextFactory.CreateWithResetTokenAsync();
        await using (context)
        {
            var repository = new PasswordResetTokenRepository(context);

            token.IsUsed = true;
            token.UsedAt = DateTime.UtcNow;
            await repository.UpdateAsync(token);
            await context.SaveChangesAsync();

            context.PasswordResetTokens.Single().IsUsed.Should().BeTrue();
            context.PasswordResetTokens.Single().UsedAt.Should().NotBeNull();
        }
    }
}
