using System.Text;

namespace LiberationFleet.Server.Application.Features.Crypto;

/// <summary>
/// Client framing for unencrypted media stored in the encrypted-content envelope
/// (<c>nonce = __plain__</c>): version byte + MIME length + MIME + raw file bytes.
/// </summary>
public static class PlainMediaFraming
{
    public const string Nonce = "__plain__";
    public const byte Version = 1;
    public const int MaxMimeLength = 256;

    public static bool IsPlainNonce(string? nonce) =>
        string.Equals(nonce?.Trim(), Nonce, StringComparison.Ordinal);

    /// <summary>
    /// Reads the plain-media header from the start of <paramref name="stream"/> without
    /// consuming it (seekable streams are rewound to their original position).
    /// </summary>
    public static bool TryGetHeader(
        Stream stream,
        out string mimeType,
        out int headerLength)
    {
        mimeType = "application/octet-stream";
        headerLength = 0;

        if (!stream.CanRead)
        {
            return false;
        }

        long origin = 0;
        if (stream.CanSeek)
        {
            origin = stream.Position;
        }

        try
        {
            Span<byte> prefix = stackalloc byte[3];
            if (!TryReadExact(stream, prefix))
            {
                return false;
            }

            if (prefix[0] != Version)
            {
                return false;
            }

            var mimeLen = prefix[1] | (prefix[2] << 8);
            if (mimeLen < 0 || mimeLen > MaxMimeLength)
            {
                return false;
            }

            if (mimeLen == 0)
            {
                headerLength = 3;
                mimeType = "application/octet-stream";
                return true;
            }

            var mimeBytes = new byte[mimeLen];
            if (!TryReadExact(stream, mimeBytes))
            {
                return false;
            }

            mimeType = Encoding.UTF8.GetString(mimeBytes);
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                mimeType = "application/octet-stream";
            }

            headerLength = 3 + mimeLen;
            return true;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = origin;
            }
        }
    }

    private static bool TryReadExact(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer.Slice(offset));
            if (read <= 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
