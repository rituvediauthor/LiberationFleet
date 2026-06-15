using LiberationFleet.Server.Infrastructure.Geocoding;

namespace LiberationFleet.Server.Tests.Infrastructure.Geocoding;

public class ZipCodeDistanceServiceTests
{
    private readonly ZipCodeDistanceService _service = new();

    [Fact]
    public void TryGetDistanceMiles_WhenBothZipsKnown_ReturnsTrueAndPositiveDistance()
    {
        var success = _service.TryGetDistanceMiles("10001", "10002", out var miles);

        success.Should().BeTrue();
        miles.Should().BeGreaterThan(0);
        miles.Should().BeLessThan(10);
    }

    [Fact]
    public void TryGetDistanceMiles_WhenZipUnknown_ReturnsFalse()
    {
        var success = _service.TryGetDistanceMiles("10001", "99999", out var miles);

        success.Should().BeFalse();
        miles.Should().Be(0);
    }

    [Fact]
    public void TryGetDistanceMiles_TrimsWhitespaceFromZipCodes()
    {
        var success = _service.TryGetDistanceMiles(" 10001 ", "10002", out var miles);

        success.Should().BeTrue();
        miles.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryGetDistanceMiles_CrossCountryDistanceIsLarge()
    {
        var success = _service.TryGetDistanceMiles("10001", "90210", out var miles);

        success.Should().BeTrue();
        miles.Should().BeGreaterThan(2000);
    }
}
