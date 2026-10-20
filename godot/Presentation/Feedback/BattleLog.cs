namespace Empire.Presentation.Feedback;

/// <summary>Ordered, bounded consecutive runs; an empty description is deliberately silent.</summary>
public sealed class BattleLog
{
    private readonly int depth;
    private readonly List<(string Line, int Times)> entries = new();

    public BattleLog(int depth = 60) { this.depth = depth; }

    public void Add(string line)
    {
        if (line.Length == 0) return;
        if (entries.Count > 0 && entries[^1].Line == line)
        {
            entries[^1] = (line, entries[^1].Times + 1);
            return;
        }
        entries.Add((line, 1));
        if (entries.Count > depth) entries.RemoveRange(0, Math.Min(entries.Count, entries.Count - depth));
    }

    public void Clear() => entries.Clear();
    public string[] Recent() => entries.Select(entry => entry.Times > 1
        ? $"{entry.Line} ×{FeedbackJson.Number(entry.Times)}" : entry.Line).ToArray();
}
