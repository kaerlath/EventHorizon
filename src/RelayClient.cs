using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventHorizon;

public sealed record DiscordChoice(string Id, string Name);
public sealed record LinkStart(string PollToken, string VerificationUrl, string Code);
public sealed record LinkResult(string Status, string? Token, string? UserName);
public sealed record PublishResult(string EventId, string MessageId, string? Warning);

public sealed class RelayClient : IDisposable
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(120) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    public string Token { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Origin { get; private set; } = "";
    public bool Connected => Token.Length > 0;
    public void Configure(string url)
    {
        if (!Uri.TryCreate(url.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
            throw new ArgumentException("Use an HTTPS relay origin (or http://localhost:port for local testing), without a path.");
        if (Origin != uri.AbsoluteUri) { Token = ""; UserName = ""; }
        Origin = uri.AbsoluteUri;
    }
    public async Task<T> Send<T>(HttpMethod method, string path, object? body = null, bool authenticated = true)
    {
        if (Origin.Length == 0) throw new InvalidOperationException("Save your relay address first.");
        using var request = new HttpRequestMessage(method, new Uri(new Uri(Origin), path));
        if (authenticated) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, lifetime.Token).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (authenticated && request.Headers.Authorization?.Parameter == Token) { Token = ""; UserName = ""; }
            throw new InvalidOperationException("Your connection expired. Disconnect and link Discord again.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(lifetime.Token).ConfigureAwait(false);
            string? detail = null;
            try { detail = JsonSerializer.Deserialize<RelayError>(content, json)?.Error; } catch (JsonException) { }
            throw new InvalidOperationException(detail ?? $"Relay returned {(int)response.StatusCode}. Check the relay address and setup.");
        }
        return await response.Content.ReadFromJsonAsync<T>(json, lifetime.Token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The relay returned an empty response.");
    }
    public void Dispose() { lifetime.Cancel(); http.Dispose(); lifetime.Dispose(); }
    private sealed record RelayError(string Error);
}
