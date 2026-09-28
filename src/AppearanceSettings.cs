namespace EventHorizon;

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "Event Horizon";
    public string Accent { get; set; } = "";
    public bool ReduceMotion { get; set; }
    public bool Glows { get; set; } = true;
    public float GlowStrength { get; set; } = .8f;
    public float AnimationSpeed { get; set; } = 1;
    public float BannerHeight { get; set; } = 340;
}
