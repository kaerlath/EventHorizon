using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace EventHorizon;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/eventhorizon";
    private readonly ICommandManager commands;
    private readonly IPluginLog log;
    private readonly IUiBuilder ui;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly WindowSystem windows = new("EventHorizon");
    private readonly MainWindow main;
    private readonly ReminderWindow reminders;
    private readonly EventConfirmationWindow confirmation;
    private readonly MinimizedWindow minimized;
    private DateTime nextBackgroundCheck;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log, ITextureProvider textures, IFramework framework, IClientState clientState)
    {
        this.commands = commands;
        this.log = log;
        this.framework = framework;
        this.clientState = clientState;
        ui = pluginInterface.UiBuilder;
        var store = new EventStore(pluginInterface.GetPluginConfigDirectory());
        // Dalamud can load assemblies from memory, where Assembly.Location is empty.
        main = new MainWindow(store, textures, pluginInterface.AssemblyLocation.Directory!.FullName, ui);
        windows.AddWindow(main);
        minimized = new MinimizedWindow(main);
        windows.AddWindow(minimized);
        reminders = new ReminderWindow(store, main);
        windows.AddWindow(reminders);
        confirmation = new EventConfirmationWindow(main);
        windows.AddWindow(confirmation);
        commands.AddHandler(Command, new CommandInfo((_, _) => { if (main.IsMinimized) main.RestoreWindow(); else main.IsOpen = !main.IsOpen; })
        {
            HelpMessage = "Open the Event Horizon event planner."
        });
        ui.Draw += Draw;
        framework.Update += Update;
        ui.OpenMainUi += Open;
        ui.OpenConfigUi += main.OpenSettings;
        if (store.LoadError is not null) log.Error("Event Horizon data could not be read: {Error}", store.LoadError);
    }

    private void Open() => main.RestoreWindow();
    private void Draw()
    {
        reminders.UpdateVisibility();
        confirmation.UpdateVisibility();
        minimized.UpdateVisibility();
        windows.Draw();
    }

    private void Update(IFramework _)
    {
        // Framework ticks continue when the planner or the game's UI is hidden.
        main.UpdatePlaySession(clientState.IsLoggedIn);
        if (DateTime.UtcNow >= nextBackgroundCheck)
        {
            nextBackgroundCheck = DateTime.UtcNow.AddSeconds(1);
            main.ProcessBackgroundWork();
        }
    }

    public void Dispose()
    {
        ui.Draw -= Draw;
        framework.Update -= Update;
        main.EndPlaySession();
        ui.OpenMainUi -= Open;
        ui.OpenConfigUi -= main.OpenSettings;
        commands.RemoveHandler(Command);
        windows.RemoveAllWindows();
        main.Dispose();
    }
}
