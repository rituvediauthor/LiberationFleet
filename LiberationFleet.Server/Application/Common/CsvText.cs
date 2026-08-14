using System.Text;

namespace LiberationFleet.Server.Application.Common;

public static class CsvText
{
    public static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    public static byte[] ToUtf8Bytes(string csv)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv);
        var combined = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, combined, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, combined, preamble.Length, body.Length);
        return combined;
    }
}
