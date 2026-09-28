using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using EventHorizon;

public interface IEventCardRenderer
{
    Task<byte[]> Render(EventRecord item, string organizer, string? banner);
}

// The relay owns the template. No URLs, HTML or browser scripts come from clients.
public sealed class BrowserRunCardRenderer : IEventCardRenderer
{
    private readonly HttpClient http;
    private readonly string account, token, font, icon;
    private string? cachedKey;
    private byte[]? cachedImage;
    public BrowserRunCardRenderer(IConfiguration config, HttpMessageHandler? handler = null)
    {
        account = config["CLOUDFLARE_ACCOUNT_ID"] ?? "";
        token = config["CLOUDFLARE_BROWSER_TOKEN"] ?? "";
        if (account.Length != 32 || !account.All(Uri.IsHexDigit) || token.Length == 0)
            throw new InvalidOperationException("Browser Run requires CLOUDFLARE_ACCOUNT_ID and CLOUDFLARE_BROWSER_TOKEN.");
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(40) };
        font = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "card-assets", "Cinzel.ttf")));
        icon = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "card-assets", "icon.png")));
    }
    public async Task<byte[]> Render(EventRecord item, string organizer, string? banner)
    {
        var html = EventCardTemplate.Html(item, organizer, banner, font, icon);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)));
        if (key == cachedKey && cachedImage is not null) return cachedImage;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/accounts/{account}/browser-rendering/screenshot");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { html, viewport = new { width = 1200, height = 900, deviceScaleFactor = 1 },
            screenshotOptions = new { type = "png", fullPage = true },
            gotoOptions = new { waitUntil = "networkidle0", timeout = 30000 } });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Browser Run returned {(int)response.StatusCode}. No Discord changes were sent.");
        if (response.Content.Headers.ContentLength > BannerUpload.MaxBytes) throw new InvalidDataException("Composed card exceeds 4 MB.");
        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (output.Length + read > BannerUpload.MaxBytes) throw new InvalidDataException("Composed card exceeds 4 MB.");
            output.Write(buffer, 0, read);
        }
        var bytes = output.ToArray();
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("Browser Run did not return a PNG. No Discord changes were sent.");
        cachedKey = key; cachedImage = bytes;
        return bytes;
    }
}

public static class EventCardTemplate
{
    public static string Html(EventRecord item, string organizer, string? banner, string font = "", string icon = "")
    {
        if (!string.IsNullOrEmpty(banner)) _ = BannerUpload.Decode(banner);
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        var local = TimeZoneInfo.ConvertTime(EventRules.Start(item), TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId));
        var end = TimeZoneInfo.ConvertTime(EventRules.Start(item).AddMinutes(item.DurationMinutes), TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId));
        var endLabel = end.ToString(end.Date == local.Date ? "h:mm tt" : "MMM d, h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        var image = string.IsNullOrEmpty(banner) ? "<div class='hero empty'>Your next gathering awaits</div>" : $"<img class='hero' src='{E(banner)}' alt='Event banner'>";
        var logo = icon.Length == 0 ? "" : $"<img class='logo' src='data:image/png;base64,{icon}' alt=''>";
        var face = font.Length == 0 ? "" : $"@font-face{{font-family:Cinzel;src:url(data:font/ttf;base64,{font})}}";
        // Fixed template and restrictive CSP prevent supplied text/images causing external requests.
        return $$"""
        <!doctype html><html><head><meta charset="utf-8">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; base-uri 'none'">
        <style>{{face}}
        *{box-sizing:border-box}body{margin:0;width:1200px;background:#060b19;color:#eef1ff;font:22px Arial,sans-serif;padding:36px}
        .card{border:1px solid #6578a4;border-radius:20px;overflow:hidden;background:linear-gradient(135deg,#101b32,#070d1c 70%)}
        header{display:flex;align-items:center;gap:18px;padding:20px 28px;border-bottom:1px solid #374668;background:#0a1125}
        .logo{width:62px;height:62px}.brand{font:32px Cinzel,Georgia,serif;letter-spacing:5px;color:#dce6ff}
        main{padding:28px}h1{font-size:38px;line-height:1.2;margin:0 0 12px;overflow-wrap:anywhere}.date{color:#b8c7e7;line-height:1.5;margin-bottom:24px}
        .hero{display:block;width:100%;height:290px;object-fit:cover;border-radius:12px;border:1px solid #3a4872}.empty{display:flex;align-items:center;justify-content:center;background:radial-gradient(ellipse,#343466,#0c1530);font:30px Cinzel,Georgia,serif;color:#adbde6}
        .columns{display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:20px;margin-top:20px}.panel{background:#142039;border:1px solid #293d5b;border-radius:12px;padding:22px}
        .description{white-space:pre-wrap;line-height:1.5;overflow-wrap:anywhere;border-left:4px solid #9d83e5}.label{text-transform:uppercase;font-size:14px;letter-spacing:2px;color:#a4b6d6;margin-bottom:8px}.value{line-height:1.4;overflow-wrap:anywhere;margin-bottom:22px}.value:last-child{margin:0}
        .groups{margin-top:20px;white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.5}.muted{color:#a4b6d6;font-size:17px}footer{display:flex;justify-content:space-between;gap:20px;padding:22px 28px;border-top:1px solid #293d5b;color:#b9c8e3;font-size:18px}
        </style></head><body><article class="card"><header>{{logo}}<div class="brand">Event Horizon</div></header><main>
        <h1>{{E(item.Title)}}</h1><div class="date">{{E(local.ToString("dddd, MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture))}}<br>
        {{E(local.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture))}} – {{E(endLabel)}} · {{E(item.TimeZoneId)}}</div>
        {{image}}<div class="columns"><div class="panel description">{{E(item.Description)}}</div><aside class="panel">
        <div class="label">Organizer</div><div class="value">{{E(organizer)}}</div><div class="label">Location</div><div class="value">{{E(item.Location)}}<br>{{E(item.World)}}</div>
        <div class="label">Community</div><div class="value">{{E(item.Server)}}</div></aside></div>
        <div class="panel groups"><div class="label">Planned signup groups</div>{{E(item.SignupGroups)}}<br><span class="muted">Use the Discord event below to mark your interest. Live role signups are not available yet.</span></div>
        </main><footer><span>From Eorzea to Discord</span><span>Event details · Event Horizon</span></footer></article></body></html>
        """;
    }
}
