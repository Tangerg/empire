#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.State;
using Godot;

namespace Empire.Presentation;

public sealed record DynamicStanding(Node2D Node, int Layer, int Index);
public sealed record DynamicStateProjection(Node2D Root, IReadOnlyList<DynamicStanding> Standing,
    IReadOnlyList<string> Diagnostics, int Structures, int Markers);

/// <summary>Read-only, replaceable view of live field objects. Authored scenery is never a state owner.</summary>
public sealed class DynamicStateView
{
    private readonly AssetCatalog assets;
    private readonly float markerOpacity = BoardDecorationDocument.Load().MarkerOpacity;
    private readonly CandidateShadowCache? shadows;
    private readonly ReadOnlyContent content;
    private const string Ink = "#28231f";
    private const string Neutral = "#918b7d";

    public DynamicStateView(AssetCatalog assets, ReadOnlyContent content, CandidateShadowCache? shadows = null)
    {
        this.assets = assets;
        this.content = content;
        this.shadows = shadows;
    }

    public Node2D CreateUnitBadges(string type, double hp, bool done, double capture, double captureThreshold,
        Vector2? directionVector = null)
    {
        var definition = content.Definition("units", type)
            ?? throw new InvalidOperationException($"Unit badges reference absent definition {type}.");
        var result = new Node2D { Name = "Badges" };
        if (directionVector is { } direction)
        {
            if (!direction.IsFinite()) throw new InvalidOperationException("Facing vector must be finite.");
            var angle = Math.Atan2(direction.Y, direction.X);
            var facing = Svg(direction == Vector2.Zero
                ? $"<circle cx=\"16\" cy=\"29.5\" r=\"1.5\" fill=\"{Ink}\" opacity=\"0.8\"/>"
                : $"<g transform=\"translate({F(16 + Math.Cos(angle) * 11)} {F(29.5 + Math.Sin(angle) * 2.25)}) rotate({F(angle * 180 / Math.PI)})\"><path d=\"M2.1 0 -1.8 -1.9 -1.8 1.9z\" fill=\"#efe6d0\" stroke=\"{Ink}\" stroke-width=\"0.55\" stroke-linejoin=\"round\" opacity=\"0.62\"/></g>", "FacingPip", new Rect2(0, 0, 32, 36));
            facing.SetMeta("direction", direction);
            result.AddChild(facing);
        }
        var maximum = Number(definition, "maxHp");
        if (maximum > 0 && hp < maximum) result.AddChild(CreateGauge(hp, maximum));
        if (capture > 0)
        {
            var ratio = Math.Min(1, capture / captureThreshold);
            var gauge = Svg($"<circle cx=\"27\" cy=\"6\" r=\"4.4\" fill=\"{Ink}\" opacity=\"0.6\"/><circle cx=\"27\" cy=\"6\" r=\"3.2\" fill=\"none\" stroke=\"#e4b84f\" stroke-width=\"2\" stroke-dasharray=\"{F(ratio * 20.1)} 20.1\" transform=\"rotate(-90 27 6)\"/>", "CaptureGauge");
            gauge.SetMeta("ratio", ratio);
            result.AddChild(gauge);
        }
        if (done) result.AddChild(Svg($"<rect width=\"32\" height=\"32\" fill=\"{Ink}\" opacity=\"0.35\"/>", "DoneShade"));
        return result;
    }

    public DynamicStateProjection Create(JsonObject state)
    {
        var root = new Node2D { Name = "DynamicState" };
        var standing = new List<DynamicStanding>();
        var diagnostics = new List<string>();
        var colors = state["players"]!.AsArray().OfType<JsonObject>().ToDictionary(player => Number(player, "id"), player => Text(player, "color"));
        var structureCount = 0;
        var markerCount = 0;
        try
        {
            foreach (var structure in state["structures"]!.AsArray().OfType<JsonObject>())
            {
                var id = Text(structure, "id");
                var type = Text(structure, "type");
                var definition = content.Definition("structures", type)
                    ?? throw new InvalidOperationException($"Live structure {id} references absent definition {type}.");
                colors.TryGetValue(Number(structure, "owner"), out var color);
                var node = new Node2D { Name = $"Structure_{structureCount}", Position = Contact(structure) };
                var hp = Number(structure, "hp");
                var maximum = Number(definition, "maxHp");
                var disabled = structure["disabled"]!.GetValue<bool>();
                var frame = hp <= maximum * 0.5 || disabled ? "damaged" : color is null ? "normal" : "captured";
                RuntimeAsset? asset = null;
                try { asset = assets.ResolveStructure(type); }
                catch (MissingArtResourceException error) { diagnostics.Add($"Structure {id}: {error.Message}; using source rule-derived drawing."); }
                if (asset is not null)
                {
                    node.AddChild(assets.CreateSprite(asset, frame));
                    node.AddChild(CandidateBoardLighting.CreateStructureContact(new(asset.FrameWidth, asset.FrameHeight), asset.PresentationScale, shadows));
                    if (color is not null) node.AddChild(CreateOwnerBanner(color, shadows));
                }
                else node.AddChild(Svg(StructureMarkup(structure, definition, color), "RuleDerivedStructure"));
                if (asset is not null || definition.GetProperty("targetable").GetBoolean())
                    node.AddChild(CreateGauge(hp, maximum));
                node.SetMeta("structure_id", id);
                node.SetMeta("structure_type", type);
                node.SetMeta("owner", (int)Number(structure, "owner"));
                node.SetMeta("hp", hp);
                node.SetMeta("max_hp", maximum);
                node.SetMeta("disabled", disabled);
                node.SetMeta("visual_state", frame);
                node.SetMeta("rule_derived", asset is null);
                root.AddChild(node);
                standing.Add(new(node, 3, structureCount++));
            }
            foreach (var marker in state["markers"]!.AsArray().OfType<JsonObject>())
            {
                var kind = Text(marker, "kind");
                colors.TryGetValue(Number(marker, "owner"), out var color);
                var node = new Node2D { Name = $"Marker_{markerCount}", Position = Contact(marker["at"]!.AsObject()), ZIndex = -1 };
                var topic = kind.Contains("corpse", StringComparison.Ordinal) || marker["fallenUnit"] is not null
                    ? "C01-BPROP-AFTERMATH-2" : kind.Contains("transport", StringComparison.Ordinal)
                        ? "C01-BPROP-AFTERMATH-3" : "C01-BPROP-EVIDENCE-1";
                try
                {
                    var asset = assets.ResolveTopic(topic);
                    var sprite = new Sprite2D
                    {
                        Modulate = new Color(1, 1, 1, markerOpacity),
                        Name = "AuthoredMarker", Texture = assets.CreateFrameTexture(asset, 0), Position = new Vector2(0, -15),
                        Scale = Vector2.One * (48f / Math.Max(asset.FrameWidth, asset.FrameHeight) / asset.PixelDensity),
                        TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                    };
                    sprite.SetMeta("topic_id", topic);
                    node.AddChild(sprite);
                }
                catch (MissingArtResourceException error)
                {
                    diagnostics.Add($"Marker {marker["id"]}: {error.Message}; using source rule-derived drawing.");
                    node.AddChild(Svg(MarkerMarkup(marker, color), "RuleDerivedMarker"));
                }
                node.SetMeta("marker_id", (int)Number(marker, "id"));
                node.SetMeta("marker_kind", kind);
                root.AddChild(node);
                markerCount++;
            }
            return new(root, standing.AsReadOnly(), diagnostics.AsReadOnly(), structureCount, markerCount);
        }
        catch { root.Free(); throw; }
    }

    public static Node2D CreateGauge(double current, double maximum)
    {
        var ratio = maximum <= 0 ? 1 : Math.Clamp(current / maximum, 0, 1);
        var color = ratio > 0.6 ? "#5fd07a" : ratio > 0.3 ? "#e8c35a" : "#e0604f";
        var node = Svg($"<rect x=\"5\" y=\"27.6\" width=\"22\" height=\"3.6\" rx=\"1.8\" fill=\"{Ink}\" opacity=\"0.65\"/>" +
            $"<rect x=\"5.6\" y=\"28.2\" width=\"{F(20.8 * ratio)}\" height=\"2.4\" rx=\"1.2\" fill=\"{color}\"/>", "HealthGauge");
        node.SetMeta("ratio", ratio);
        return node;
    }

    public static Sprite2D CreateOwnerBanner(string color, CandidateShadowCache? shadows = null)
    {
        var banner = Svg(OwnerBanner(color), "OwnerBanner", new Rect2(-24, -8, 80, 60), shadows);
        banner.SetMeta("owner_color", color);
        if (shadows is not null) banner.AddChild(CandidateSpriteShadows.Create(banner, CandidateShadowKind.OwnerBanner, 2, shadows));
        return banner;
    }

    private static Vector2 Contact(JsonObject at) => new((float)Number(at, "x") * 32 + 16, (float)Number(at, "y") * 32 + 31);
    private static string Text(JsonElement value, string field) => Empire.Core.Determinism.EcmaJson.ReadString(JsonValue.Create(value.GetProperty(field)));
    private static double Number(JsonElement value, string field) => value.GetProperty(field).GetDouble();
    private static string Text(JsonObject value, string field) => value[field]!.GetValue<string>();
    private static double Number(JsonObject value, string field) => double.Parse(value[field]!.ToJsonString(), CultureInfo.InvariantCulture);
    private static string F(double value) => (Math.Floor(value * 100 + 0.5) / 100).ToString("0.##", CultureInfo.InvariantCulture);
    private static double NameHash(string name, int salt)
    {
        var hash = 0x811c9dc5u ^ (uint)salt;
        foreach (var letter in name) hash = unchecked((hash ^ letter) * 0x01000193u);
        return (hash >> 8) / 16777216.0;
    }
    private static string Shade(string hex, double amount)
    {
        var color = Color.FromHtml(hex);
        int Mix(float channel) => (int)Math.Clamp(Math.Floor((amount < 0 ? channel * 255 * (1 + amount) : channel * 255 + (255 - channel * 255) * amount) + 0.5), 0, 255);
        return $"#{Mix(color.R):x2}{Mix(color.G):x2}{Mix(color.B):x2}";
    }
    private static Sprite2D Svg(string markup, string name, Rect2? bounds = null, CandidateShadowCache? cache = null)
    {
        var view = bounds ?? new Rect2(0, 0, 32, 32);
        var source = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(view.Size.X)}\" height=\"{F(view.Size.Y)}\" viewBox=\"{F(view.Position.X)} {F(view.Position.Y)} {F(view.Size.X)} {F(view.Size.Y)}\">{markup}</svg>";
        Sprite2D sprite;
        if (cache is not null)
        {
            sprite = new Sprite2D { Texture = cache.SvgSource(source, 2) };
        }
        else
        {
            using var image = new Image();
            var error = image.LoadSvgFromString(source, 2);
            if (error != Error.Ok)
                throw new InvalidOperationException($"Dynamic source drawing failed: {name}, {error}");
            sprite = OwnedProceduralSprite.Create(image);
        }
        sprite.Name = name;
        sprite.Centered = false;
        sprite.Offset = (view.Position - new Vector2(16, 31)) * 2;
        sprite.Scale = Vector2.One / 2;
        sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        return sprite;
    }

    private static string OwnerBanner(string color) =>
        $"<defs><radialGradient id=\"held\" cx=\"0.5\" cy=\"0.5\" r=\"0.5\"><stop offset=\"0\" stop-color=\"{color}\" stop-opacity=\"0.3\"/><stop offset=\"0.55\" stop-color=\"{color}\" stop-opacity=\"0.12\"/><stop offset=\"1\" stop-color=\"{color}\" stop-opacity=\"0\"/></radialGradient></defs>" +
        $"<rect x=\"-24\" y=\"-8\" width=\"80\" height=\"60\" fill=\"url(#held)\"/><path d=\"M7.4 20.5L7.4 5.2\" stroke=\"#241b14\" stroke-width=\"1.3\" stroke-linecap=\"round\"/><path d=\"M7.4 5.2L7.4 4.1\" stroke=\"{Shade(color, 0.1)}\" stroke-width=\"1.9\" stroke-linecap=\"round\"/>" +
        $"<path d=\"M8.1 5.6L16.6 8.1L8.1 11.1Z\" fill=\"{Shade(color, 0.1)}\" stroke=\"{Shade(color, -0.45)}\" stroke-width=\"0.55\" stroke-linejoin=\"round\"/><path d=\"M8.1 5.6L16.6 8.1L8.1 11.1Z\" fill=\"none\" stroke=\"#ffffff\" stroke-opacity=\"0.22\" stroke-width=\"0.35\"/>";

    // Direct translation of the original field-objects-from-rules.ts fallback; no authored art is fabricated.
    private static string StructureMarkup(JsonObject state, JsonElement definition, string? ownerColor)
    {
        var id = Text(state, "type");
        var maximum = Number(definition, "maxHp");
        var ratio = maximum <= 0 ? 1 : Math.Clamp(Number(state, "hp") / maximum, 0, 1);
        var raised = Math.Max(Number(definition, "obstructionHeight"), Text(definition, "cover") switch { "half" => 1.4, "full" => 2.8, _ => 0 });
        var tones = new[] { "#85837d", "#77786f", "#948d80", "#6f7370", "#9c948a" };
        var disabled = state["disabled"]!.GetValue<bool>();
        var face = Shade(tones[(int)Math.Floor(NameHash(id, 0) * tones.Length) % tones.Length], disabled ? -0.32 : 0);
        var top = Shade(face, 0.24); var side = Shade(face, -0.3);
        var height = Math.Min(23, 9 + raised * 4); var crown = 27 - height;
        var footing = Math.Floor((7.5 + NameHash(id, 3) * 3) * 100 + 0.5) / 100;
        var blocks = definition.GetProperty("blocksMovement").GetBoolean();
        var output = new StringBuilder();
        if (blocks)
        {
            output.Append($"<path d=\"M2 27V{F(crown)}h28v27z\" fill=\"{face}\" stroke=\"{Ink}\" stroke-width=\"0.8\"/><path d=\"M2 {F(crown)}h28l-2.6 2.6H4.6z\" fill=\"{top}\"/><path d=\"M27.4 {F(crown + 2.6)}H30V27h-2.6z\" fill=\"{side}\"/>");
            var courses = 3 + Math.Floor(NameHash(id, 2) * 2 + 0.5);
            for (var row = 0; row < courses; row++)
            {
                var y = crown + 3.4 + row * (height - 4) / courses;
                if (y > 26) continue;
                output.Append($"<path d=\"M3 {F(y)}h26\" stroke=\"{side}\" stroke-width=\"0.8\" opacity=\"0.6\"/><path d=\"M{(row % 2 == 0 ? 9 : 16)} {F(y)}v{F((height - 4) / courses)}\" stroke=\"{side}\" stroke-width=\"0.8\" opacity=\"0.5\"/>");
            }
        }
        else
        {
            var lean = NameHash(id, 1) > 0.5 ? 1 : -1;
            output.Append($"<ellipse cx=\"16\" cy=\"27\" rx=\"{F(footing + 2)}\" ry=\"2.6\" fill=\"{Ink}\" opacity=\"0.3\"/><path d=\"M{F(16 - footing)} 27V{F(crown + 4)}q0-4 {F(footing)}-4t{F(footing)} 4V27z\" fill=\"{face}\" stroke=\"{Ink}\" stroke-width=\"0.8\"/><ellipse cx=\"16\" cy=\"{F(crown + 4)}\" rx=\"{F(footing)}\" ry=\"3\" fill=\"{top}\"/><path d=\"M{16 + lean * 5} {F(crown + 3)}v-{F(Math.Max(3, height - 8))}\" stroke=\"{side}\" stroke-width=\"2.4\"/><circle cx=\"{16 + lean * 5}\" cy=\"{F(crown)}\" r=\"2.6\" fill=\"{ownerColor ?? Neutral}\" stroke=\"{Ink}\" stroke-width=\"0.6\"/>");
        }
        output.Append($"<rect x=\"2\" y=\"{F(Math.Min(24, crown + height - 6))}\" width=\"28\" height=\"2.4\" fill=\"{ownerColor ?? Neutral}\" opacity=\"{(blocks ? "0.85" : "0")}\"/>");
        for (var index = 0; index < Math.Floor((1 - ratio) * 3 + 0.5); index++)
            output.Append($"<path d=\"M{F(7 + NameHash(id, 40 + index) * 18)} {F(9 + NameHash(id, 50 + index) * 12)}l{F(1.5 - NameHash(id, 60 + index) * 3)} 4l2 3\" stroke=\"{Ink}\" stroke-width=\"1\" fill=\"none\" opacity=\"0.55\"/>");
        if (disabled) output.Append($"<path d=\"M8 {F(crown + 3)}l16 16m0-16-16 16\" stroke=\"{Ink}\" stroke-width=\"2\" opacity=\"0.5\"/>");
        return output.ToString();
    }

    private static string MarkerMarkup(JsonObject marker, string? ownerColor)
    {
        var tint = ownerColor ?? Neutral;
        var turn = F(-30 + NameHash(Text(marker, "kind"), 2) * 60);
        return marker["fallenUnit"] is not null
            ? $"<g transform=\"rotate({turn} 16 22)\" opacity=\"0.82\"><ellipse cx=\"16\" cy=\"24\" rx=\"9\" ry=\"3\" fill=\"{Ink}\" opacity=\"0.3\"/><path d=\"M9 23q3-4 8-3.6t6 3.6z\" fill=\"{Shade(tint, -0.28)}\" stroke=\"{Ink}\" stroke-width=\"0.7\"/><circle cx=\"8.6\" cy=\"21.4\" r=\"2.6\" fill=\"#b97d5b\" stroke=\"{Ink}\" stroke-width=\"0.6\"/><path d=\"M21 20.6l5-3.4\" stroke=\"#737b79\" stroke-width=\"1.6\" stroke-linecap=\"round\"/></g>"
            : $"<g transform=\"rotate({turn} 16 24)\" opacity=\"0.9\"><ellipse cx=\"16\" cy=\"25\" rx=\"5.4\" ry=\"1.8\" fill=\"{Ink}\" opacity=\"0.3\"/><rect x=\"15.2\" y=\"13\" width=\"1.6\" height=\"12\" fill=\"#513622\"/><path d=\"M16.8 13.4h6l-1.9 2.6 1.9 2.6h-6z\" fill=\"{tint}\" stroke=\"{Ink}\" stroke-width=\"0.5\"/></g>";
    }
}
