using LiberationFleet.Server.Infrastructure.Security;

namespace LiberationFleet.Server.Tests.Infrastructure.Security;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsDifferentValueThanPlainTextPassword()
    {
        var hash = _hasher.Hash("password123");

        hash.Should().NotBe("password123");
        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Hash_GeneratesUniqueHashesForSamePassword()
    {
        var hash1 = _hasher.Hash("password123");
        var hash2 = _hasher.Hash("password123");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_WhenPasswordMatchesHash_ReturnsTrue()
    {
        const string password = "password123";
        var hash = _hasher.Hash(password);

        _hasher.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WhenPasswordDoesNotMatchHash_ReturnsFalse()
    {
        var hash = _hasher.Hash("password123");

        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }
}
