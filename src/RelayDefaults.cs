using System.Text.Json;

namespace EventHorizon;

public static class RelayDefaults
{
    public static string Read(string directory)
    {
        var path = Path.Combine(directory, "relay-defaults.json");
        if (!File.Exists(path)) return "";
        using var data = JsonDocument.Parse(File.ReadAllText(path));
        var value = data.RootElement.GetProperty("RelayUrl").GetString() ?? "";
        if (value.Length == 0) return "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo != "" || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "")
            throw new InvalidDataException("The distributed relay address must be an HTTPS origin.");
        return uri.AbsoluteUri;
    }
}
