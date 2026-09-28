using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EventHorizon;

public sealed partial class MainWindow
{
    internal bool IsMinimized { get; private set; }
    internal Vector2 MinimizedPosition { get; private set; }
    private Vector2 expandedPosition, expandedSize;
    private bool restoringGeometry;
    private void MinimizeToIcon()
    {
        if (ConfirmationPending) return;
        expandedPosition = ImGui.GetWindowPos(); expandedSize = ImGui.GetWindowSize();
        MinimizedPosition = expandedPosition;
        IsMinimized = true; IsOpen = false;
    }
    public void RestoreWindow()
    {
        if (IsMinimized)
        {
            Position = expandedPosition; PositionCondition = ImGuiCond.Always;
            Size = expandedSize / Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale; SizeCondition = ImGuiCond.Always;
            restoringGeometry = true; IsMinimized = false;
        }
        IsOpen = true;
    }
    private void FinishRestoreGeometry()
    {
        if (!restoringGeometry) return;
        restoringGeometry = false; Position = null; Size = null;
        PositionCondition = SizeCondition = ImGuiCond.FirstUseEver;
    }
    internal void DrawMinimizedIcon()
    {
        var start = ImGui.GetCursorScreenPos();
        DrawOrb(78); ImGui.SetCursorScreenPos(start);
        if (ImGui.InvisibleButton("Restore Event Horizon", new Vector2(78))) RestoreWindow();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to restore Event Horizon");
    }
}

internal sealed class MinimizedWindow : Window
{
    private readonly MainWindow main;
    internal MinimizedWindow(MainWindow main) : base("Event Horizon icon###EventHorizonMinimized",
        ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove)
    {
        this.main = main; ShowCloseButton = false;
        Size = new Vector2(78); SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
    }
    internal void UpdateVisibility()
    {
        IsOpen = main.IsMinimized;
        Position = main.MinimizedPosition; PositionCondition = ImGuiCond.Always;
    }
    public override void PreDraw()
    {
        Size = new Vector2(78) / Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }
    public override void PostDraw() => ImGui.PopStyleVar();
    public override void Draw() => main.DrawMinimizedIcon();
}
