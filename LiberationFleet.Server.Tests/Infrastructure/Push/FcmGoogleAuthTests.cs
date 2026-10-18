using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using LiberationFleet.Server.Infrastructure.Push;

namespace LiberationFleet.Server.Tests.Infrastructure.Push;

public class FcmGoogleAuthTests
{
    [Fact]
    public void CreateJwtAssertion_UsesShortClaimNamesGoogleAccepts()
    {
        using var rsa = RSA.Create(2048);
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_email"] = "firebase-adminsdk@liberationfleet-test.iam.gserviceaccount.com",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["token_uri"] = "https://oauth2.googleapis.com/token"
        });

        var (assertion, tokenUri) = FcmGoogleAuth.CreateJwtAssertion(json);

        tokenUri.Should().Be("https://oauth2.googleapis.com/token");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(assertion);
        token.Issuer.Should().Be("firebase-adminsdk@liberationfleet-test.iam.gserviceaccount.com");
        token.Subject.Should().Be("firebase-adminsdk@liberationfleet-test.iam.gserviceaccount.com");
        token.Audiences.Should().Contain("https://oauth2.googleapis.com/token");
        token.Claims.Should().Contain(c => c.Type == "scope" && c.Value == FcmGoogleAuth.FirebaseMessagingScope);
        assertion.Should().NotContain("schemas.xmlsoap.org");
    }
}
