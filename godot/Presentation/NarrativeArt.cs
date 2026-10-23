namespace Empire.Presentation;

public sealed class NarrativeArt
{
    public string Source { get; init; } = "";
    public string? ResourcePath
    {
        get; init;
    }
    public string? Sha256
    {
        get; init;
    }
}
