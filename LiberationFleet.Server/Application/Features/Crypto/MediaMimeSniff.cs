using System.Text;

namespace LiberationFleet.Server.Application.Features.Crypto;

/// <summary>
/// Sniff media MIME from magic bytes so progressive &lt;video&gt;/&lt;audio&gt; gets a
/// playable Content-Type (Safari/Chrome often refuse application/octet-stream).
/// </summary>
public static class MediaMimeSniff
{
    public static bool IsGenericMime(string? mime) =>
        string.IsNullOrWhiteSpace(mime)
        || string.Equals(Normalize(mime), "application/octet-stream", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Normalize(mime), "binary/octet-stream", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strip codecs parameters and map common aliases to browser-friendly types.
    /// </summary>
    public static string Normalize(string? mime)
    {
        var trimmed = (mime ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        var baseType = trimmed.Split(';', 2)[0].Trim();
        return baseType switch
        {
            "audio/m4a" or "audio/x-m4a" => "audio/mp4",
            "audio/mp3" => "audio/mpeg",
            "audio/wave" or "audio/x-wav" => "audio/wav",
            _ => baseType
        };
    }

    /// <summary>
    /// Peek the first bytes of a seekable stream (rewinds to the original position).
    /// </summary>
    public static string? TrySniff(Stream stream, bool preferAudio = false)
    {
        if (stream is null || !stream.CanRead || !stream.CanSeek)
        {
            return null;
        }

        var origin = stream.Position;
        try
        {
            Span<byte> buffer = stackalloc byte[16];
            var read = stream.Read(buffer);
            if (read < 12)
            {
                return null;
            }

            return Sniff(buffer[..read], preferAudio);
        }
        finally
        {
            stream.Position = origin;
        }
    }

    /// <summary>
    /// Prefer a concrete media type when the declared MIME is generic, or when an
    /// AudioAsset WebM would otherwise be labeled video/webm.
    /// </summary>
    public static string Resolve(string? declared, Stream contentStream, bool preferAudio = false)
    {
        var trimmed = Normalize(declared);
        preferAudio = preferAudio || trimmed.StartsWith("audio/", StringComparison.Ordinal);
        var sniffed = TrySniff(contentStream, preferAudio);

        if (IsGenericMime(trimmed))
        {
            return sniffed ?? "application/octet-stream";
        }

        if (string.Equals(trimmed, "video/quicktime", StringComparison.OrdinalIgnoreCase)
            && sniffed is "video/mp4")
        {
            return "video/mp4";
        }

        // Voice notes / audio uploads: keep audio/* even if EBML looks like video/webm.
        if (preferAudio && trimmed.StartsWith("video/", StringComparison.Ordinal))
        {
            return "audio/" + trimmed["video/".Length..];
        }

        return trimmed;
    }

    public static string? Sniff(ReadOnlySpan<byte> view, bool preferAudio = false)
    {
        if (view.Length < 12)
        {
            return null;
        }

        // JPEG
        if (view[0] == 0xff && view[1] == 0xd8 && view[2] == 0xff)
        {
            return "image/jpeg";
        }

        // PNG
        if (view[0] == 0x89 && view[1] == 0x50 && view[2] == 0x4e && view[3] == 0x47)
        {
            return "image/png";
        }

        // GIF
        if (view[0] == 0x47 && view[1] == 0x49 && view[2] == 0x46 && view[3] == 0x38)
        {
            return "image/gif";
        }

        // WEBP: RIFF....WEBP
        if (view[0] == 0x52 && view[1] == 0x49 && view[2] == 0x46 && view[3] == 0x46
            && view[8] == 0x57 && view[9] == 0x45 && view[10] == 0x42 && view[11] == 0x50)
        {
            return "image/webp";
        }

        // MP4 / MOV / M4A (ftyp box)
        if (view[4] == 0x66 && view[5] == 0x74 && view[6] == 0x79 && view[7] == 0x70)
        {
            var brand = Encoding.ASCII.GetString(view.Slice(8, Math.Min(4, view.Length - 8))).ToLowerInvariant();
            if (brand.Contains("m4a", StringComparison.Ordinal)
                || brand.Contains("mp4a", StringComparison.Ordinal)
                || preferAudio)
            {
                return "audio/mp4";
            }

            if (brand.StartsWith("qt", StringComparison.Ordinal) || brand.Contains("qt", StringComparison.Ordinal))
            {
                // Still serve as video/mp4 when possible — many browsers refuse quicktime over HTTP.
                return "video/mp4";
            }

            return "video/mp4";
        }

        // WebM / Matroska — EBML header alone cannot distinguish audio-only containers.
        if (view[0] == 0x1a && view[1] == 0x45 && view[2] == 0xdf && view[3] == 0xa3)
        {
            return preferAudio ? "audio/webm" : "video/webm";
        }

        // OGG
        if (view[0] == 0x4f && view[1] == 0x67 && view[2] == 0x67 && view[3] == 0x53)
        {
            return "audio/ogg";
        }

        // WAV
        if (view[0] == 0x52 && view[1] == 0x49 && view[2] == 0x46 && view[3] == 0x46
            && view[8] == 0x57 && view[9] == 0x41 && view[10] == 0x56 && view[11] == 0x45)
        {
            return "audio/wav";
        }

        // MP3 ID3 or frame sync
        if (view[0] == 0x49 && view[1] == 0x44 && view[2] == 0x33)
        {
            return "audio/mpeg";
        }

        if (view[0] == 0xff && (view[1] & 0xe0) == 0xe0)
        {
            return "audio/mpeg";
        }

        return null;
    }
}
