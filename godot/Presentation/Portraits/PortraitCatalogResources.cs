namespace Empire.Presentation;

public static class PortraitCatalogResources
{
    public static PortraitCatalog Load()
    {
        const string path = "res://data/portrait-catalog.json";
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            throw new InvalidOperationException($"Cannot read portrait catalog {path}: {Godot.FileAccess.GetOpenError()}");
        return PortraitCatalog.Parse(file.GetAsText());
    }
}
