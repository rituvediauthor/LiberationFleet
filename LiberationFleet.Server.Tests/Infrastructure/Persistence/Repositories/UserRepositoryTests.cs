using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class UserRepositoryTests
{
    [Fact]
    public async Task ExistsByEmailOrUsernameAsync_WhenEmailExists_ReturnsTrue()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync(email: "exists@example.com");
        var repository = new UserRepository(context);

        var exists = await repository.ExistsByEmailOrUsernameAsync("exists@example.com", "otheruser");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByEmailOrUsernameAsync_WhenUsernameExists_ReturnsTrue()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync(username: "existinguser");
        var repository = new UserRepository(context);

        var exists = await repository.ExistsByEmailOrUsernameAsync("other@example.com", "existinguser");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByEmailOrUsernameAsync_WhenNeitherExists_ReturnsFalse()
    {
        await using var context = TestDbContextFactory.Create();
        var repository = new UserRepository(context);

        var exists = await repository.ExistsByEmailOrUsernameAsync("missing@example.com", "missinguser");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByEmailOrUsernameAsync_FindsUserByEmail()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync(email: "findme@example.com");
        var repository = new UserRepository(context);

        var user = await repository.GetByEmailOrUsernameAsync("findme@example.com");

        user.Should().NotBeNull();
        user!.Email.Should().Be("findme@example.com");
    }

    [Fact]
    public async Task GetByEmailOrUsernameAsync_FindsUserByUsername()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync(username: "findmeuser");
        var repository = new UserRepository(context);

        var user = await repository.GetByEmailOrUsernameAsync("findmeuser");

        user.Should().NotBeNull();
        user!.Username.Should().Be("findmeuser");
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsMatchingUser()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync(email: "emailonly@example.com");
        var repository = new UserRepository(context);

        var user = await repository.GetByEmailAsync("emailonly@example.com");

        user.Should().NotBeNull();
        user!.Email.Should().Be("emailonly@example.com");
    }

    [Fact]
    public async Task AddAsync_PersistsUserAfterSaveChanges()
    {
        await using var context = TestDbContextFactory.Create();
        var repository = new UserRepository(context);

        var user = new User
        {
            Username = "addeduser",
            Email = "added@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        context.Users.Should().ContainSingle(u => u.Email == "added@example.com");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesAfterSaveChanges()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var repository = new UserRepository(context);
        var user = context.Users.Single();

        user.Username = "updateduser";
        await repository.UpdateAsync(user);
        await context.SaveChangesAsync();

        context.Users.Single().Username.Should().Be("updateduser");
    }

    [Fact]
    public async Task GetByIdWithProfileAsync_ReturnsUserWithPaymentPlatforms()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync();
        var user = context.Users.Single();
        context.UserPaymentPlatforms.Add(new Domain.Entities.UserPaymentPlatform
        {
            UserId = user.Id,
            PaymentPlatformId = 1,
            Handle = "user@example.com"
        });
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var loaded = await repository.GetByIdWithProfileAsync(user.Id);

        loaded.Should().NotBeNull();
        loaded!.PaymentPlatforms.Should().ContainSingle(p => p.PaymentPlatformId == 1);
    }

    [Fact]
    public async Task IsUsernameTakenByOtherUserAsync_WhenAnotherUserHasUsername_ReturnsTrue()
    {
        await using var context = await TestDbContextFactory.CreateWithUserAsync(username: "existing");
        var repository = new UserRepository(context);
        var otherUserId = context.Users.Single().Id + 99;

        var taken = await repository.IsUsernameTakenByOtherUserAsync("existing", otherUserId);

        taken.Should().BeTrue();
    }
}
