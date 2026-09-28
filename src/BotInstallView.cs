using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private sealed record BotInstallLink(string Url);
    private void DrawBotInstallation()
    {
        ImGui.TextColored(Accent, "ADD THE BOT TO YOUR SERVER");
        ImGui.TextWrapped("A server owner or a member with Manage Server permission can install Event Horizon. Discord will let you choose the server. This is separate from linking your personal Discord account.");
        ImGui.BeginDisabled(relay.Origin.Length == 0);
        if (ImGui.Button("Add Event Horizon bot to a server", new Vector2(360, 40))) RequestBotInstallation(false);
        if (ImGui.Button("Copy bot installation link", new Vector2(280, 36))) RequestBotInstallation(true);
        ImGui.EndDisabled();
        ImGui.TextWrapped("After approving the installation in Discord, return here, connect your account if needed, and click Load servers. Then choose the server and announcement channel. If you cannot install bots, share the link with a server administrator.");
        ImGui.Separator();
    }
    private void RequestBotInstallation(bool copy)
    {
        Run(async () =>
        {
            var link = await relay.Send<BotInstallLink>(HttpMethod.Get, "discord/install", authenticated: false);
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "discord.com" || uri.Port != 443 || uri.AbsolutePath != "/oauth2/authorize" || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
                throw new InvalidOperationException("The relay returned an unexpected bot installation address.");
            return () =>
            {
                if (copy) { ImGui.SetClipboardText(link.Url); message = "Bot installation link copied. You can share it with a server administrator."; }
                else { message = "Choose your server and approve installation in Discord. Then return and click Load servers."; OpenUrl(link.Url); }
            };
        });
    }
}
