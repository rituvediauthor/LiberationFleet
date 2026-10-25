using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using LiberationFleet.Server.Infrastructure.Push;

namespace LiberationFleet.Server.Tests.Infrastructure.Push;

public class ApnsAuthTests
{
    [Fact]
    public void CreateJwt_UsesShortIssIatAndKidAppleAccepts()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportPkcs8PrivateKeyPem();

        var jwt = ApnsAuth.CreateJwt("TEAMID1234", "KEYID5678", pem);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        token.Header.Alg.Should().Be("ES256");
        token.Header.Kid.Should().Be("KEYID5678");
        token.Issuer.Should().Be("TEAMID1234");
        token.Payload.IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        jwt.Should().NotContain("schemas.xmlsoap.org");
    }

    [Fact]
    public void NormalizePem_UnescapesKeyVaultNewlines()
    {
        var normalized = ApnsAuth.NormalizePem("\"-----BEGIN PRIVATE KEY-----\\nABC\\n-----END PRIVATE KEY-----\"");

        normalized.Should().Contain("-----BEGIN PRIVATE KEY-----\nABC\n-----END PRIVATE KEY-----");
        normalized.Should().NotContain("\\n");
    }
}
