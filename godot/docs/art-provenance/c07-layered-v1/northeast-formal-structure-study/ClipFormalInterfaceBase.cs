using Godot;
using Empire.Presentation;
using System.Text.Json.Nodes;

// Offline projection: clip the frozen formal triangles, retaining their original affine UV map.
public partial class ClipFormalInterfaceBase : Node
{
    public override void _Ready()
    {
        try
        {
            var source = PresentationJson.Load<JsonObject>("res://northeast-baseline-surface.json");
            var registration = PresentationJson.Load<JsonObject>("res://northeast-frozen-v2-sections.json");
            var design = PresentationJson.Load<JsonObject>("res://northeast-interface-design-contract.json");
            var frame = PresentationJson.Load<JsonObject>("res://northeast-baseline-registration.json");
            var origin = Point(frame["localOrigin"]!);
            var scale = registration["scale"]!.GetValue<float>();
            var translation = Point(registration["translation"]!);
            var cut = design["replacementDomainTexturePixels"]!.AsArray()
                .Select(p => Point(p!) * scale + translation - origin).ToArray();
            var output = new JsonArray();
            int retained = 0, changed = 0;
            foreach (var member in source["members"]!.AsArray().OfType<JsonObject>())
            {
                var polygon = member["polygonPixels"]!.AsArray().Select(p => Point(p!)).ToArray();
                if (Geometry2D.IntersectPolygons(polygon, cut).Count == 0)
                {
                    output.Add(member.DeepClone()); retained++; continue;
                }
                changed++;
                var uv = member["uvPixels"]!.AsArray().Select(p => Point(p!)).ToArray();
                var triangles = Geometry2D.TriangulatePolygon(polygon);
                int piece = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i], ib = triangles[i+1], ic = triangles[i+2];
                    var triangle = new[] { polygon[ia], polygon[ib], polygon[ic] };
                    foreach (var clipped in Geometry2D.ClipPolygons(triangle, cut))
                    {
                        if (clipped.Length < 3) throw new Exception("Degenerate retained triangle fragment.");
                        if (Geometry2D.IsPolygonClockwise(clipped) != Geometry2D.IsPolygonClockwise(triangle))
                            throw new Exception("Inner ring cannot be represented as a filled retained face.");
                        var row = member.DeepClone().AsObject();
                        row["id"] = member["id"]!.GetValue<string>() + "-clip-" + piece++;
                        row["polygonPixels"] = Points(clipped);
                        row["uvPixels"] = Points(clipped.Select(p => ClippedTriangleUv.Map(p, triangle[0], triangle[1], triangle[2], uv[ia], uv[ib], uv[ic])));
                        output.Add(row);
                    }
                }
            }
            source["members"] = output;
            File.WriteAllText(ProjectSettings.GlobalizePath("res://northeast-interface-affine-base-surface.json"), source.ToJsonString(new() { WriteIndented = true }));
            GD.Print($"FORMAL_TRIANGLE_INTERFACE_CLIP_COMPLETE untouched={retained} changed={changed} members={output.Count}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static Vector2 Point(JsonNode p) => new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
    private static JsonArray Points(IEnumerable<Vector2> points) => new(points.Select(p => (JsonNode)new JsonArray(p.X,p.Y)).ToArray());
}
