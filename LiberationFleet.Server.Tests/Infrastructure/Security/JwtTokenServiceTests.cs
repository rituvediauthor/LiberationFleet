using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiberationFleet.Server.Infrastructure.Security;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;

namespace LiberationFleet.Server.Tests.Infrastructure.Security;

public class JwtTokenServiceTests
{
    [Fact]
    public void Constructor_WhenSecretKeyMissing_ThrowsInvalidOperationException()
    {
        var configuration = new ConfigurationBuilder().Build();

        var act = () => new JwtTokenService(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT SecretKey not configured");
    }

    [Fact]
    public void GenerateJwtToken_ReturnsValidTokenWithExpectedClaims()
    {
        var configuration = TestConfiguration.CreateJwtConfiguration();
        var service = new JwtTokenService(configuration);
        var user = HandlerTestFixture.CreateUser(id: 42, username: "fleetuser", email: "fleet@example.com");

        var token = service.GenerateJwtToken(user);

        token.Should().NotBeNullOrWhiteSpace();

        var principal = service.ValidateJwtToken(token);
        principal.Should().NotBeNull();
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42");
        principal.FindFirst(ClaimTypes.Email)!.Value.Should().Be("fleet@example.com");
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("fleetuser");
    }

    [Fact]
    public void ValidateJwtToken_WhenTokenIsMalformed_ReturnsNull()
    {
        var configuration = TestConfiguration.CreateJwtConfiguration();
        var service = new JwtTokenService(configuration);

        var principal = service.ValidateJwtToken("not-a-valid-jwt");

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateJwtToken_WhenTokenSignedWithDifferentKey_ReturnsNull()
    {
        var issuerService = new JwtTokenService(TestConfiguration.CreateJwtConfiguration());
        var otherService = new JwtTokenService(TestConfiguration.CreateJwtConfiguration(secretKey: "DifferentSecretKeyThatIsAlsoLongEnough123456"));
        var user = HandlerTestFixture.CreateUser();

        var token = issuerService.GenerateJwtToken(user);
        var principal = otherService.ValidateJwtToken(token);

        principal.Should().BeNull();
    }

    [Fact]
    public void GenerateJwtToken_TokenCanBeParsedAsJwtSecurityToken()
    {
        var service = new JwtTokenService(TestConfiguration.CreateJwtConfiguration());
        var user = HandlerTestFixture.CreateUser();

        var token = service.GenerateJwtToken(user);
        var handler = new JwtSecurityTokenHandler();

        handler.CanReadToken(token).Should().BeTrue();
        var jwt = handler.ReadJwtToken(token);
        jwt.Issuer.Should().Be("LiberationFleetTest");
        jwt.Audiences.Should().Contain("LiberationFleetTestClient");
    }
}
