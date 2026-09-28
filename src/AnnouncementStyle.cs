namespace EventHorizon;

public sealed class AnnouncementStyle
{
    public TextAppearance Title { get; set; } = new() { Size = 38, Bold = true };
    public TextAppearance Body { get; set; } = new();
    // Keys are zero-based paragraphs separated by a blank line in Description.
    public Dictionary<int, TextAppearance> Paragraphs { get; set; } = new();
    public AnnouncementStyle Copy() => new() { Title = Title?.Copy() ?? new() { Size = 38, Bold = true }, Body = Body?.Copy() ?? new(), Paragraphs = (Paragraphs ?? new()).Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value.Copy()) };
}

public sealed class TextAppearance
{
    public string Font { get; set; } = "Sans";
    public int Size { get; set; } = 22;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strike { get; set; }
    public string Color { get; set; } = "#EDF1FF";
    public string Align { get; set; } = "left";
    public float Outline { get; set; }
    public string OutlineColor { get; set; } = "#060B19";
    public float Glow { get; set; }
    public string GlowColor { get; set; } = "#A695FF";
    public float LetterSpacing { get; set; }
    public float LineHeight { get; set; } = 1.5f;
    public TextAppearance Copy() => (TextAppearance)MemberwiseClone();
}
