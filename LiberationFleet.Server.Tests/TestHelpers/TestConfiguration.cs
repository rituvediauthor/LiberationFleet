using Microsoft.Extensions.Configuration;

namespace LiberationFleet.Server.Tests.TestHelpers;

public static class TestConfiguration
{
    public static IConfiguration CreateJwtConfiguration(
        string secretKey = "ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256",
        string issuer = "LiberationFleetTest",
        string audience = "LiberationFleetTestClient",
        int expirationMinutes = 10080)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = secretKey,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString(),
                // AddInfrastructure registers email; tests run as Production-like unless set.
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Email:SmtpHost"] = ""
            })
            .Build();
    }
}
