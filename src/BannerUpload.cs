namespace EventHorizon;

public static class BannerUpload
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public static (byte[] Bytes, string Mime, string FileName)? Decode(string? data)
    {
        if (string.IsNullOrEmpty(data)) return null;
        if (data.Length > MaxBytes * 4 / 3 + 100) throw new ArgumentException("Banner must be smaller than 4 MB.");
        var comma = data.IndexOf(',');
        if (comma < 0) throw new ArgumentException("Invalid banner image.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(data[(comma + 1)..]); }
        catch (FormatException) { throw new ArgumentException("Invalid banner image encoding."); }
        var mime = Mime(bytes);
        if (data[..comma] != $"data:{mime};base64") throw new ArgumentException("Banner format does not match its contents.");
        return (bytes, mime, mime == "image/png" ? "event-banner.png" : "event-banner.jpg");
    }
    public static string Encode(byte[] bytes) => $"data:{Mime(bytes)};base64,{Convert.ToBase64String(bytes)}";
    private static string Mime(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new ArgumentException("Banner must be smaller than 4 MB.");
        if (bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        throw new ArgumentException("Choose a PNG or JPEG image.");
    }
}
