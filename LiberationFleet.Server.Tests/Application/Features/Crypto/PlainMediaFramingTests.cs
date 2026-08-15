using System.Text;
using FluentAssertions;
using LiberationFleet.Server.Application.Features.Crypto;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto;

public class PlainMediaFramingTests
{
    [Fact]
    public void TryGetHeader_ParsesMimeAndRewindsStream()
    {
        var fileBytes = new byte[] { 9, 9, 9, 9 };
        var mime = "video/mp4";
        var mimeBytes = Encoding.UTF8.GetBytes(mime);
        var framed = new byte[3 + mimeBytes.Length + fileBytes.Length];
        framed[0] = PlainMediaFraming.Version;
        framed[1] = (byte)(mimeBytes.Length & 0xff);
        framed[2] = (byte)((mimeBytes.Length >> 8) & 0xff);
        Buffer.BlockCopy(mimeBytes, 0, framed, 3, mimeBytes.Length);
        Buffer.BlockCopy(fileBytes, 0, framed, 3 + mimeBytes.Length, fileBytes.Length);

        using var stream = new MemoryStream(framed);
        PlainMediaFraming.TryGetHeader(stream, out var mimeType, out var headerLength).Should().BeTrue();
        mimeType.Should().Be("video/mp4");
        headerLength.Should().Be(3 + mimeBytes.Length);
        stream.Position.Should().Be(0);

        using var bounded = new BoundedReadStream(stream, headerLength, fileBytes.Length);
        var payload = new byte[fileBytes.Length];
        bounded.Read(payload, 0, payload.Length).Should().Be(fileBytes.Length);
        payload.Should().Equal(fileBytes);
        bounded.Length.Should().Be(fileBytes.Length);
    }

    [Fact]
    public void IsPlainNonce_MatchesSentinel()
    {
        PlainMediaFraming.IsPlainNonce("__plain__").Should().BeTrue();
        PlainMediaFraming.IsPlainNonce("  __plain__ ").Should().BeTrue();
        PlainMediaFraming.IsPlainNonce("aes-nonce").Should().BeFalse();
    }

    [Fact]
    public void BoundedReadStream_SupportsSeekForRangeRequests()
    {
        var data = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        using var inner = new MemoryStream(data);
        using var bounded = new BoundedReadStream(inner, 10, 20);
        bounded.Seek(5, SeekOrigin.Begin);
        bounded.ReadByte().Should().Be(15);
        bounded.Position.Should().Be(6);
    }
}
