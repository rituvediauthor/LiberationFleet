using Microsoft.Extensions.Configuration;

namespace LiberationFleet.Server.Tests.TestHelpers;

public static class TestConfiguration
{
    public static IConfiguration CreateJwtConfiguration(
        string secretKey = "ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256",
        string issuer = "LiberationFleetTest",
        string audience = "LiberationFleetTestClient")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = secretKey,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience
            })
            .Build();
    }
}
