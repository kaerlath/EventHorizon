using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using EventHorizon;

var builder = WebApplication.CreateBuilder(args);
// Request URLs contain short-lived OAuth codes: do not log them.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 90, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 6 * 1024 * 1024);
var app = builder.Build();
var relay = new Relay(builder.Configuration);
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    try { await next(); }
    catch (RelayException ex) { context.Response.StatusCode = ex.Status; await context.Response.WriteAsJsonAsync(new { error = ex.Message }); }
    catch (Exception ex)
    {
        // Never send OAuth credentials or Discord response bodies to the client or log.
        app.Logger.LogError("Relay operation failed ({Type})", ex.GetType().Name);
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { error = "The relay could not complete the request. Retry later; existing publication IDs are retained." });
    }
});
app.MapGet("/health", () => new { service = "Event Horizon", version = "0.4.1", configured = relay.Configured, composition = relay.CompositionMode });
app.MapPost("/auth/link", () => relay.StartLink());
app.MapGet("/auth/authorize/{key}", (string key) => Results.Content(relay.VerificationPage(key), "text/html"));
app.MapPost("/auth/authorize/{key}", (string key) => Results.Redirect(relay.Authorization(key)));
app.MapGet("/auth/callback", async (string? code, string? state, string? error) =>
{
    if (error is not null || code is null || state is null) return Results.Text("Discord linking was cancelled. Return to Event Horizon and try again.");
    await relay.Callback(code, state);
    return Results.Text("Discord linked to Event Horizon. Return to the game and click Finish connection. You can close this tab.");
});
app.MapPost("/auth/poll", (PollRequest request) => relay.Poll(request.PollToken));
app.MapPost("/auth/logout", (HttpContext context) => { relay.Logout(context); return new { ok = true }; });
app.MapGet("/discord/guilds", async (HttpContext context) => await relay.Guilds(relay.Session(context)));
app.MapGet("/discord/guilds/{id}/channels", async (HttpContext context, string id) => await relay.Channels(relay.Session(context), id));
app.MapGet("/events", async (HttpContext context) => await relay.History(relay.Session(context)));
app.MapPut("/events/{id:guid}/publish", async (HttpContext context, Guid id, PublishRequest request) =>
    await relay.Publish(relay.Session(context), id, request.Event, request.BannerDataUrl));
app.Run();

public sealed record PollRequest(string PollToken);
public sealed record PublishRequest(EventRecord Event, string? BannerDataUrl);
public sealed record Choice(string Id, string Name);
public sealed record Login(string UserId, string Name, string AccessToken, DateTimeOffset Expires);
public sealed class PendingLink
{
    public string PollToken { get; init; } = Relay.RandomToken();
    public string Key { get; init; } = Relay.RandomToken();
    public string Code { get; init; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    public DateTimeOffset Expires { get; init; } = DateTimeOffset.UtcNow.AddMinutes(5);
    public Login? Login { get; set; }
}
public sealed class Published
{
    public string UserId { get; set; } = "";
    public Guid Id { get; set; }
    public string GuildId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string EventId { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string BannerAttachmentId { get; set; } = "";
    public string BannerFileName { get; set; } = "";
    public string? SourceBannerDataUrl { get; set; }
    public bool EventPending { get; set; }
    public bool MessagePending { get; set; }
    public EventRecord? Record { get; set; }
}
public class RelayException(string message, int status = 400) : Exception(message) { public int Status { get; } = status; }

public sealed class Relay
{
    private const ulong Administrator = 1UL << 3, ManageEvents = 1UL << 33, CreateEvents = 1UL << 44;
    private const ulong ChannelPermissions = (1UL << 10) | (1UL << 11) | (1UL << 14) | (1UL << 15);
    private readonly string clientId, secret, bot, origin, dataPath;
    private readonly HttpClient http;
    private readonly IEventCardRenderer? cardRenderer;
    public string CompositionMode => cardRenderer is null ? "discord-embed" : "browser-run";
    private readonly ConcurrentDictionary<string, PendingLink> links = new();
    private readonly ConcurrentDictionary<string, PendingLink> states = new();
    private readonly ConcurrentDictionary<string, Login> sessions = new();
    private readonly SemaphoreSlim publishLock = new(1, 1);
    private readonly List<Published> events;
    public bool Configured => clientId.Length > 0 && secret.Length > 0 && bot.Length > 0;
    public Relay(IConfiguration config, HttpMessageHandler? handler = null, IEventCardRenderer? renderer = null)
    {
        var mode = config["CARD_RENDERER"] ?? "discord-embed";
        if (mode is not ("discord-embed" or "browser-run")) throw new InvalidOperationException("CARD_RENDERER must be browser-run or discord-embed.");
        cardRenderer = renderer ?? (mode == "browser-run" ? new BrowserRunCardRenderer(config) : null);
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
        clientId = config["DISCORD_CLIENT_ID"] ?? "";
        secret = config["DISCORD_CLIENT_SECRET"] ?? "";
        bot = config["DISCORD_BOT_TOKEN"] ?? "";
        origin = (config["PUBLIC_ORIGIN"] ?? "http://localhost:5187").TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.IsLoopback && uri.Scheme == "http")) || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "")
            throw new InvalidOperationException("PUBLIC_ORIGIN must be an HTTPS origin, or HTTP loopback for development.");
        dataPath = Path.GetFullPath(config["EVENT_DATA_PATH"] ?? "data/events.json");
        Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
        // Fail closed on damaged data rather than losing idempotency records.
        events = File.Exists(dataPath) ? JsonSerializer.Deserialize<List<Published>>(File.ReadAllText(dataPath))
            ?? throw new InvalidDataException("Event data is empty.") : new();
    }
    public static string RandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private void Cleanup()
    {
        foreach (var entry in links.Where(x => x.Value.Expires < DateTimeOffset.UtcNow)) links.TryRemove(entry.Key, out _);
        foreach (var entry in states.Where(x => x.Value.Expires < DateTimeOffset.UtcNow)) states.TryRemove(entry.Key, out _);
        foreach (var entry in sessions.Where(x => x.Value.Expires < DateTimeOffset.UtcNow)) sessions.TryRemove(entry.Key, out _);
    }
    public object StartLink()
    {
        if (!Configured) throw new RelayException("Configure the relay's Discord application ID, client secret and bot token first.", 503);
        Cleanup();
        if (links.Count > 1000) throw new RelayException("Too many pending connections. Try later.", 429);
        var link = new PendingLink(); links[link.PollToken] = link;
        return new { link.PollToken, verificationUrl = $"{origin}/auth/authorize/{link.Key}", link.Code };
    }
    public string Authorization(string key)
    {
        var link = links.Values.FirstOrDefault(x => x.Key == key && x.Expires > DateTimeOffset.UtcNow && x.Login is null)
            ?? throw new RelayException("This link expired. Start again in Event Horizon.");
        var state = RandomToken(); states[state] = link;
        return "https://discord.com/oauth2/authorize?response_type=code&scope=identify%20guilds&client_id=" + clientId +
            "&redirect_uri=" + Uri.EscapeDataString(origin + "/auth/callback") + "&state=" + state + "&prompt=consent";
    }
    public string VerificationPage(string key)
    {
        var link = links.Values.FirstOrDefault(x => x.Key == key && x.Expires > DateTimeOffset.UtcNow && x.Login is null)
            ?? throw new RelayException("This link expired. Start again in Event Horizon.");
        return $$"""
            <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
            <title>Connect Event Horizon</title><style>body{background:#0b0d18;color:#eceafa;font:18px system-ui;max-width:640px;margin:12vh auto;padding:24px}h1{color:#b0a0ff}strong{display:block;font-size:36px;letter-spacing:6px;padding:24px 0}button{background:#6952aa;color:white;border:0;padding:16px;border-radius:8px;font:inherit}</style>
            <h1>Event Horizon</h1><p>Connect the game session showing this reference:</p><strong>{{link.Code}}</strong>
            <p>Continue only if this matches the reference in your Event Horizon plugin and you started the connection yourself.</p>
            <form method="post"><button>Continue to Discord</button></form></html>
            """;
    }
    public async Task Callback(string code, string state)
    {
        if (!states.TryRemove(state, out var link) || link.Expires < DateTimeOffset.UtcNow || link.Login is not null)
            throw new RelayException("Invalid or expired authorization. Start again in the plugin.");
        using var response = await http.PostAsync("https://discord.com/api/v10/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["client_id"] = clientId, ["client_secret"] = secret, ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = origin + "/auth/callback" }));
        if (!response.IsSuccessStatusCode) throw new RelayException("Discord did not authorize the account. Start again.", 401);
        var token = await response.Content.ReadFromJsonAsync<JsonElement>();
        var access = token.GetProperty("access_token").GetString()!;
        var user = await Discord(HttpMethod.Get, "users/@me", access, false);
        link.Login = new Login(user.GetProperty("id").GetString()!, user.GetProperty("username").GetString()!, access,
            DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, token.GetProperty("expires_in").GetInt32())));
    }
    public object Poll(string token)
    {
        if (!links.TryGetValue(token, out var link) || link.Expires < DateTimeOffset.UtcNow)
            throw new RelayException("Connection request expired. Start again.");
        if (link.Login is null) return new { status = "pending" };
        if (!links.TryRemove(token, out _)) throw new RelayException("Connection already completed.");
        var session = RandomToken(); sessions[session] = link.Login;
        return new { status = "connected", token = session, userName = link.Login.Name };
    }
    private static string Bearer(HttpContext context) => context.Request.Headers.Authorization.ToString() is var header && header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : "";
    public Login Session(HttpContext context)
    {
        if (!sessions.TryGetValue(Bearer(context), out var login) || login.Expires <= DateTimeOffset.UtcNow)
            throw new RelayException("Please link your Discord account again.", 401);
        return login;
    }
    public void Logout(HttpContext context) => sessions.TryRemove(Bearer(context), out _);
    public static bool CanCreate(ulong permissions) => (permissions & (Administrator | ManageEvents | CreateEvents)) != 0;
    public async Task<Choice[]> Guilds(Login login)
    {
        var guilds = await AllGuilds(login.AccessToken, false);
        var botGuilds = await AllGuilds(bot, true);
        var installed = botGuilds.Select(x => x.GetProperty("id").GetString()).ToHashSet();
        return guilds.Where(g => installed.Contains(g.GetProperty("id").GetString()) &&
            (g.GetProperty("owner").GetBoolean() || CanCreate(ulong.Parse(g.GetProperty("permissions").GetString()!))))
            .Select(g => new Choice(g.GetProperty("id").GetString()!, g.GetProperty("name").GetString()!)).ToArray();
    }
    private async Task<List<JsonElement>> AllGuilds(string token, bool isBot)
    {
        var result = new List<JsonElement>();
        string after = "0";
        for (var page = 0; page < 20; page++)
        {
            var data = (await Discord(HttpMethod.Get, $"users/@me/guilds?limit=200&after={after}", token, isBot)).EnumerateArray().ToArray();
            result.AddRange(data);
            if (data.Length < 200) return result;
            var next = data.Max(x => ulong.Parse(x.GetProperty("id").GetString()!)).ToString();
            if (next == after) break;
            after = next;
        }
        throw new RelayException("Could not finish loading your server list. Try again later.", 503);
    }
    public async Task<EventRecord[]> History(Login login)
    {
        var allowed = (await Guilds(login)).Select(x => x.Id).ToHashSet();
        await publishLock.WaitAsync();
        try
        {
            return events.Where(x => x.UserId == login.UserId && allowed.Contains(x.GuildId) && x.Record is not null && x.EventId != "")
                .Select(x => { var record = x.Record!.Copy(); record.DiscordEventId = x.EventId; record.DiscordMessageId = x.MessageId;
                    record.RelayOrigin = origin + "/"; record.Status = "Published"; return record; }).ToArray();
        }
        finally { publishLock.Release(); }
    }
    private async Task AuthorizeGuild(Login login, string id)
    {
        if (!(await Guilds(login)).Any(x => x.Id == id)) throw new RelayException("You need Create Events permission and the Event Horizon bot in this server.", 403);
    }
    public async Task<Choice[]> Channels(Login login, string guild)
    {
        await AuthorizeGuild(login, guild);
        var channels = await Discord(HttpMethod.Get, $"guilds/{guild}/channels", bot);
        var roles = await Discord(HttpMethod.Get, $"guilds/{guild}/roles", bot);
        var member = await Discord(HttpMethod.Get, $"guilds/{guild}/members/{login.UserId}", bot);
        var botUser = await Discord(HttpMethod.Get, "users/@me", bot);
        var botId = botUser.GetProperty("id").GetString()!;
        var botMember = await Discord(HttpMethod.Get, $"guilds/{guild}/members/{botId}", bot);
        var guildInfo = await Discord(HttpMethod.Get, $"guilds/{guild}", bot);
        bool owner = guildInfo.GetProperty("owner_id").GetString() == login.UserId;
        return channels.EnumerateArray().Where(c => c.GetProperty("type").GetInt32() is 0 or 5 &&
            (owner || CanPost(Permissions(guild, login.UserId, member, roles, c))) &&
            CanPost(Permissions(guild, botId, botMember, roles, c)))
            .Select(c => new Choice(c.GetProperty("id").GetString()!, c.GetProperty("name").GetString()!)).ToArray();
    }
    private static bool CanPost(ulong p) => (p & Administrator) != 0 || (p & ChannelPermissions) == ChannelPermissions;
    public static ulong Permissions(string guild, string user, JsonElement member, JsonElement roles, JsonElement channel)
    {
        var ids = member.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
        ulong p = 0;
        foreach (var role in roles.EnumerateArray()) if (role.GetProperty("id").GetString() == guild || ids.Contains(role.GetProperty("id").GetString()!)) p |= ulong.Parse(role.GetProperty("permissions").GetString()!);
        if ((p & Administrator) != 0) return ulong.MaxValue;
        var overwrites = channel.GetProperty("permission_overwrites").EnumerateArray().ToArray();
        foreach (var o in overwrites.Where(x => x.GetProperty("id").GetString() == guild)) p = Apply(p, o);
        ulong allow = 0, deny = 0;
        foreach (var o in overwrites.Where(x => x.GetProperty("type").GetInt32() == 0 && ids.Contains(x.GetProperty("id").GetString()!)))
        { allow |= ulong.Parse(o.GetProperty("allow").GetString()!); deny |= ulong.Parse(o.GetProperty("deny").GetString()!); }
        p = (p & ~deny) | allow;
        foreach (var o in overwrites.Where(x => x.GetProperty("type").GetInt32() == 1 && x.GetProperty("id").GetString() == user)) p = Apply(p, o);
        return p;
    }
    private static ulong Apply(ulong p, JsonElement o) => (p & ~ulong.Parse(o.GetProperty("deny").GetString()!)) | ulong.Parse(o.GetProperty("allow").GetString()!);
    private void Save()
    {
        File.WriteAllText(dataPath + ".tmp", JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(dataPath + ".tmp", dataPath, true);
    }
    public async Task<object> Publish(Login login, Guid id, EventRecord item, string? bannerDataUrl = null)
    {
        if (item is null) throw new RelayException("An event is required.");
        (byte[] Bytes, string Mime, string FileName)? banner;
        try { banner = BannerUpload.Decode(bannerDataUrl); }
        catch (ArgumentException ex) { throw new RelayException(ex.Message); }
        if (item.Id != id) throw new RelayException("Event ID mismatch.");
        if (EventRules.Validate(item, true) is { } error) throw new RelayException(error);
        await AuthorizeGuild(login, item.GuildId);
        if (item.ChannelId != "" && !(await Channels(login, item.GuildId)).Any(c => c.Id == item.ChannelId))
            throw new RelayException("You and the bot must be able to view, send messages, embed links and attach files in the announcement channel.", 403);
        await publishLock.WaitAsync();
        try
        {
            if (events.Any(e => e.Id == id && e.UserId != login.UserId))
                throw new RelayException("This event belongs to a different Discord account. Reconnect the original account, or create a new copy.", 403);
            var saved = events.FirstOrDefault(e => e.Id == id && e.UserId == login.UserId);
            if (saved is null)
            {
                saved = new Published { Id = id, UserId = login.UserId, GuildId = item.GuildId, ChannelId = item.ChannelId };
                events.Add(saved);
            }
            if (saved.GuildId != item.GuildId || saved.ChannelId != item.ChannelId)
                throw new RelayException("Published events cannot change server or channel. Create a copy instead.");
            if (saved.EventPending) throw new RelayException("A previous Discord create has an uncertain result. The relay operator must reconcile this record before retrying; no duplicate was sent.", 409);
            var effectiveBanner = bannerDataUrl ?? saved.SourceBannerDataUrl;
            if (cardRenderer is not null && effectiveBanner is null && saved.BannerFileName.Length > 0)
                throw new RelayException("Select the banner again once to enable composed cards for this older event.");
            (byte[] Bytes, string Mime, string FileName)? announcementBanner = banner;
            // Render before changing Discord. A quota/renderer failure leaves the existing post intact.
            if (cardRenderer is not null && item.ChannelId.Length > 0)
                announcementBanner = (await cardRenderer.Render(item, string.IsNullOrWhiteSpace(item.Organizer) ? login.Name : item.Organizer, effectiveBanner), "image/png", "event-card.png");
            var start = EventRules.Start(item);
            var location = string.IsNullOrWhiteSpace(item.World) ? item.Location : item.World + " — " + item.Location;
            var payload = new Dictionary<string, object?> { ["name"] = item.Title, ["description"] = item.Description,
                ["scheduled_start_time"] = start, ["scheduled_end_time"] = start.AddMinutes(item.DurationMinutes),
                ["privacy_level"] = 2, ["entity_type"] = 3, ["channel_id"] = null, ["entity_metadata"] = new { location } };
            // Omitted means preserve the cover, empty means explicitly remove it.
            if (bannerDataUrl is not null) payload["image"] = bannerDataUrl.Length == 0 ? null : bannerDataUrl;
            if (saved.EventId == "")
            {
                saved.EventPending = true; Save();
                try
                {
                    var created = await Discord(HttpMethod.Post, $"guilds/{item.GuildId}/scheduled-events", bot, true, payload);
                    saved.EventId = created.GetProperty("id").GetString()!; saved.EventPending = false; Save();
                }
                catch (DiscordRejectedException) { saved.EventPending = false; Save(); throw; }
            }
            else await Discord(HttpMethod.Patch, $"guilds/{item.GuildId}/scheduled-events/{saved.EventId}", bot, true, payload);
            saved.Record = item; saved.SourceBannerDataUrl = effectiveBanner; Save();
            string? warning = null;
            if (item.ChannelId != "")
            {
                if (saved.MessagePending) warning = "Event saved. An earlier announcement has an uncertain result; ask the relay operator to reconcile it.";
                else
                {
                    var eventUrl = $"https://discord.com/events/{item.GuildId}/{saved.EventId}";
                    var fields = new List<object>
                    {
                        new { name = "Date & time", value = $"<t:{start.ToUnixTimeSeconds()}:F>\n<t:{start.ToUnixTimeSeconds()}:t> – <t:{start.AddMinutes(item.DurationMinutes).ToUnixTimeSeconds()}:t> · <t:{start.ToUnixTimeSeconds()}:R>", inline = false },
                        new { name = "Location", value = location, inline = true },
                        new { name = "Organizer", value = string.IsNullOrWhiteSpace(item.Organizer) ? login.Name : item.Organizer, inline = true }
                    };
                    if (!string.IsNullOrWhiteSpace(item.SignupGroups)) fields.Add(new { name = "Signup groups · planned", value = item.SignupGroups, inline = false });
                    var embed = new Dictionary<string, object?> { ["title"] = item.Title, ["description"] = item.Description, ["url"] = eventUrl,
                        ["color"] = 9278463, ["fields"] = fields, ["footer"] = new { text = "Event Horizon • Join the event on Discord" } };
                    if (cardRenderer is not null)
                        embed = new Dictionary<string, object?> { ["url"] = eventUrl, ["color"] = 9278463 };
                    var attachmentName = announcementBanner?.FileName ?? (bannerDataUrl == "" ? "" : saved.BannerFileName);
                    if (attachmentName.Length > 0) embed["image"] = new { url = "attachment://" + attachmentName };
                    var message = new Dictionary<string, object?> { ["allowed_mentions"] = new { parse = Array.Empty<string>() }, ["embeds"] = new[] { embed },
                        ["components"] = new[] { new { type = 1, components = new[] { new { type = 2, style = 5, label = "View event / Interested", url = eventUrl } } } } };
                    if (announcementBanner is { } upload) message["attachments"] = new[] { new { id = "0", filename = upload.FileName, description = item.Title + " · " + item.StartLocal + " · " + item.Location } };
                    else if (bannerDataUrl == "") message["attachments"] = Array.Empty<object>();
                    else if (saved.BannerAttachmentId.Length > 0) message["attachments"] = new[] { new { id = saved.BannerAttachmentId, filename = saved.BannerFileName } };
                    try
                    {
                        if (saved.MessageId == "")
                        {
                            saved.MessagePending = true; Save();
                            var sent = await Discord(HttpMethod.Post, $"channels/{item.ChannelId}/messages", bot, true, message, announcementBanner);
                            RememberBanner(saved, sent, cardRenderer is not null ? "composed" : bannerDataUrl, announcementBanner);
                            saved.MessageId = sent.GetProperty("id").GetString()!; saved.MessagePending = false; Save();
                        }
                        else
                        {
                            var edited = await Discord(HttpMethod.Patch, $"channels/{item.ChannelId}/messages/{saved.MessageId}", bot, true, message, announcementBanner);
                            RememberBanner(saved, edited, cardRenderer is not null ? "composed" : bannerDataUrl, announcementBanner); Save();
                        }
                    }
                    catch (DiscordRejectedException) { saved.MessagePending = false; Save(); warning = "Event saved, but Discord rejected the announcement. Check channel permissions and retry Save & Sync."; }
                    catch (HttpRequestException) { warning = "Event saved. Announcement delivery could not be confirmed; ask the relay operator to reconcile it."; }
                    catch (TaskCanceledException) { warning = "Event saved. Announcement timed out; ask the relay operator to reconcile it."; }
                }
            }
            return new { eventId = saved.EventId, messageId = saved.MessageId, warning };
        }
        finally { publishLock.Release(); }
    }
    private static void RememberBanner(Published saved, JsonElement response, string? data, (byte[] Bytes, string Mime, string FileName)? banner)
    {
        if (data == "") { saved.BannerAttachmentId = ""; saved.BannerFileName = ""; }
        else if (banner is { } upload && response.TryGetProperty("attachments", out var attachments))
        {
            foreach (var attachment in attachments.EnumerateArray())
                if (attachment.GetProperty("filename").GetString() == upload.FileName)
                { saved.BannerAttachmentId = attachment.GetProperty("id").GetString()!; saved.BannerFileName = upload.FileName; break; }
        }
    }
    private async Task<JsonElement> Discord(HttpMethod method, string path, string token, bool isBot = true, object? body = null,
        (byte[] Bytes, string Mime, string FileName)? banner = null)
    {
        using var request = new HttpRequestMessage(method, "https://discord.com/api/v10/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue(isBot ? "Bot" : "Bearer", token);
        if (banner is { } file)
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"), "payload_json");
            var content = new ByteArrayContent(file.Bytes); content.Headers.ContentType = new MediaTypeHeaderValue(file.Mime);
            multipart.Add(content, "files[0]", file.FileName); request.Content = multipart;
        }
        else if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var message = status switch { 429 => "Discord rate limited this request. Wait before retrying.", 403 => "Discord denied access. Check bot permissions.", 401 => "Discord authorization expired or the bot credential is invalid.", _ => $"Discord returned HTTP {status}." };
            if (status is >= 400 and < 500) throw new DiscordRejectedException(message, status);
            throw new RelayException(message, 503);
        }
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
public sealed class DiscordRejectedException(string message, int status) : RelayException(message, status);
