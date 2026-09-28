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
    private readonly WindowSystem windows = new("EventHorizon");
    private readonly MainWindow main;
    private readonly ReminderWindow reminders;
    private readonly EventConfirmationWindow confirmation;
    private DateTime nextBackgroundCheck;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log, ITextureProvider textures)
    {
        this.commands = commands;
        this.log = log;
        ui = pluginInterface.UiBuilder;
        var store = new EventStore(pluginInterface.GetPluginConfigDirectory());
        // Dalamud can load assemblies from memory, where Assembly.Location is empty.
        main = new MainWindow(store, textures, pluginInterface.AssemblyLocation.Directory!.FullName, ui);
        windows.AddWindow(main);
        reminders = new ReminderWindow(store, main);
        windows.AddWindow(reminders);
        confirmation = new EventConfirmationWindow(main);
        windows.AddWindow(confirmation);
        commands.AddHandler(Command, new CommandInfo((_, _) => main.IsOpen = !main.IsOpen)
        {
            HelpMessage = "Open the Event Horizon event planner."
        });
        ui.Draw += Draw;
        ui.OpenMainUi += Open;
        ui.OpenConfigUi += main.OpenSettings;
        if (store.LoadError is not null) log.Error("Event Horizon data could not be read: {Error}", store.LoadError);
    }

    private void Open() => main.IsOpen = true;
    private void Draw()
    {
        // The plugin callback remains active even when MainWindow.IsOpen is false.
        if (DateTime.UtcNow >= nextBackgroundCheck)
        {
            nextBackgroundCheck = DateTime.UtcNow.AddSeconds(1);
            main.ProcessBackgroundWork();
        }
        reminders.UpdateVisibility();
        confirmation.UpdateVisibility();
        windows.Draw();
    }

    public void Dispose()
    {
        ui.Draw -= Draw;
        ui.OpenMainUi -= Open;
        ui.OpenConfigUi -= main.OpenSettings;
        commands.RemoveHandler(Command);
        windows.RemoveAllWindows();
        main.Dispose();
    }
}
