using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private bool gameLoggedIn;
    private int connectionGeneration;
    private DateTime nextHeartbeat;
    private Task? heartbeatTask;
    private string heartbeatToken = "";
    private string connectionNotice = "";
    private bool? heartbeatMode;

    public void UpdatePlaySession(bool loggedIn)
    {
        if (gameLoggedIn && !loggedIn) EndPlaySession();
        gameLoggedIn = loggedIn;
        if (heartbeatTask is { IsCompleted: true })
        {
            try { heartbeatTask.GetAwaiter().GetResult(); connectionNotice = ""; }
            catch (Exception ex) { if (relay.Token == heartbeatToken) connectionNotice = ex.Message; }
            heartbeatTask = null;
        }
        if (!loggedIn || !relay.Connected || heartbeatTask is not null) return;
        var enabled = store.StayConnectedWhilePlaying;
        if (heartbeatToken == relay.Token && heartbeatMode == enabled && DateTime.UtcNow < nextHeartbeat) return;
        // With renewal off, tell the relay once, then let its one-hour lease expire.
        if (!enabled && heartbeatToken == relay.Token && heartbeatMode == false && connectionNotice.Length == 0) return;
        heartbeatToken = relay.Token;
        heartbeatMode = enabled;
        nextHeartbeat = DateTime.UtcNow.AddMinutes(2);
        heartbeatTask = relay.Send<object>(HttpMethod.Post, "auth/heartbeat", new { enabled });
    }

    public void EndPlaySession()
    {
        connectionGeneration++;
        var token = relay.Token;
        var origin = relay.Origin;
        relay.Token = "";
        relay.UserName = "";
        link = null;
        guilds = [];
        channels = [];
        heartbeatMode = null;
        if (token.Length > 0) _ = RevokeSession(origin, token);
    }

    private static async Task RevokeSession(string origin, string token)
    {
        // Unload cannot await network I/O. A separate bounded request survives disposal;
        // the relay's short lease is the fallback if the game/process has already exited.
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(origin), "auth/logout"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request).ConfigureAwait(false);
        }
        catch { /* Expiry cleans up a session when logout cannot reach the relay. */ }
    }

    private void DrawPlaySessionSettings()
    {
        var enabled = store.StayConnectedWhilePlaying;
        if (ImGui.Checkbox("Stay connected while playing", ref enabled))
        {
            store.StayConnectedWhilePlaying = enabled;
            TrySaveSettings();
        }
        ImGui.TextWrapped("Keeps Discord connected while logged into the game, even with this window closed. Logout or unloading the plugin disconnects. After a crash or lost connection, the relay session expires within 15 minutes of its last renewal. Turning this off restores a connection of up to one hour.");
        if (connectionNotice.Length > 0) ImGui.TextWrapped(connectionNotice);
    }
}
