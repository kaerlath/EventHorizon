namespace EventHorizon;

public sealed class AppearanceSettings
{
    public float CalendarFontScale { get; set; } = 1;
    public string CalendarTextColor { get; set; } = "#B9C6E8";
    public Dictionary<string, CalendarTextStyle> CalendarEventStyles { get; set; } = new();
    public string Theme { get; set; } = "Event Horizon";
    public string Accent { get; set; } = "";
    public bool ReduceMotion { get; set; }
    public bool Glows { get; set; } = true;
    public float GlowStrength { get; set; } = .8f;
    public float AnimationSpeed { get; set; } = 1;
    public float BannerHeight { get; set; } = 340;
    public float BackgroundOpacity { get; set; } = 1;
}

public sealed class CalendarTextStyle
{
    public float Scale { get; set; } = 1;
    public string Color { get; set; } = "#B9C6E8";
}
