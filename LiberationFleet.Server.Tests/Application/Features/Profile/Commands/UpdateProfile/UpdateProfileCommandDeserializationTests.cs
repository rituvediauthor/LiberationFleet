using System.Text.Json;
using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

namespace LiberationFleet.Server.Tests.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_WithTempPaymentPlatformId_Succeeds()
    {
        const string json = """
            {
              "username": "James",
              "email": "james@example.com",
              "paymentPlatforms": [
                { "id": -1, "platformId": 1, "platform": "PayPal", "handle": "james@example.com" }
              ]
            }
            """;

        var command = JsonSerializer.Deserialize<UpdateProfileCommand>(json, JsonOptions);

        command.Should().NotBeNull();
        command!.PaymentPlatforms.Should().ContainSingle(p => p.Id == -1 && p.PlatformId == 1);
    }

    [Fact]
    public void Deserialize_WithTimestampStylePaymentPlatformId_ThrowsJsonException()
    {
        const string json = """
            {
              "username": "James",
              "email": "james@example.com",
              "paymentPlatforms": [
                { "id": -1748000000000, "platform": "PayPal", "handle": "james@example.com" }
              ]
            }
            """;

        var act = () => JsonSerializer.Deserialize<UpdateProfileCommand>(json, JsonOptions);

        act.Should().Throw<JsonException>();
    }
}
