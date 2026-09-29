using System.Globalization;
namespace EventHorizon;

public static class CalendarText
{
    // Split oversized words at text-element boundaries, preserving surrogate pairs and accents.
    public static string[] Wrap(string text, float width, Func<string, float> measure)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (measure(candidate) <= width) { line = candidate; continue; }
                if (line.Length > 0) { lines.Add(line); line = ""; }
                var elements = StringInfo.GetTextElementEnumerator(word);
                while (elements.MoveNext())
                {
                    var element = elements.GetTextElement();
                    if (line.Length > 0 && measure(line + element) > width) { lines.Add(line); line = ""; }
                    line += element;
                }
            }
            lines.Add(line);
        }
        return lines.ToArray();
    }
}
