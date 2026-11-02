using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;

public partial class PlatformBaseExport : Node
{
    public override void _Ready()
    {
        try
        {
            var basis = PresentationJson.Load<JsonObject>("res://northwest-lookout-base.json");
            var platform = PresentationJson.Load<JsonObject>("res://mount-platform.json");
            Vector2 Pair(JsonNode p) => new(p[0]!.GetValue<float>(), p[1]!.GetValue<float>());
            var polygons = platform["members"]!.AsArray().Select(m => m!["polygon"]!.AsArray().Select(p => Pair(p!)).ToArray()).ToArray();
            var boundary = polygons[0];
            foreach (var polygon in polygons.Skip(1))
            {
                var union = Geometry2D.MergePolygons(boundary, polygon);
                if (union.Count != 1)
                    throw new InvalidOperationException("Tower cap must form one connected replacement boundary without holes.");
                boundary = union[0];
            }
            var scale = basis["scale"]!.GetValue<float>();
            var translation = Pair(basis["translation"]!);
            basis["excludePolygons"]!.AsArray().Add(new JsonArray(boundary.Select(p =>
            {
                var local = (p - translation) / scale;
                return (JsonNode)new JsonArray(local.X, local.Y);
            }).ToArray()));
            basis["replacementOwner"] = "Union of new cap/upper-tower polygons replaces old base, derived from the platform source; no extra extent or depth offset.";
            File.WriteAllText(ProjectSettings.GlobalizePath("res://northwest-platform-base.json"), basis.ToJsonString(new() { WriteIndented = true }));
            GD.Print($"PLATFORM_BASE_REPLACEMENT_COMPLETE vertices={boundary.Length}");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PushError(ex.ToString());
            GetTree().Quit(1);
        }
    }
}
