using LiberationFleet.Server.Infrastructure.Serialization;
using System.Text.Json;

namespace LiberationFleet.Server.Tests.Infrastructure.Serialization;

public class UtcDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Fact]
    public void Serialize_UnspecifiedDateTime_WritesUtcZSuffix()
    {
        var value = new DateTime(2026, 9, 8, 14, 40, 0, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(value, Options);

        json.Should().Be("\"2026-09-08T14:40:00.0000000Z\"");
    }

    [Fact]
    public void Serialize_NullableUnspecified_WritesUtcZSuffix()
    {
        DateTime? value = new DateTime(2026, 9, 8, 14, 40, 0, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(value, Options);

        json.Should().Be("\"2026-09-08T14:40:00.0000000Z\"");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new UtcNullableDateTimeJsonConverter());
        return options;
    }
}
