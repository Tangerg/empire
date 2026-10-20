#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>Source scene-frame and illustrated cover projections; no terrain or cover rules.</summary>
internal static class CandidateBoardScenery
{
    internal sealed record Placement(string Id, double X, double Y, double Scale, bool Flip,
        double Opacity, CandidateShadowKind Shadow);
    private static readonly string[] Trees = { "pine-tall", "pine-mature-a", "pine-mature-b", "pine-mature-c",
        "pine-cluster", "pine-pair", "oak-ancient", "mixed-forest-dense" };

    // Contacts are field-local. The source viewport includes 74/62/74/74 woodland insets.
    internal static IReadOnlyList<Placement> Perimeter(int width, int height)
    {
        var result = new List<Placement>();
        var sceneWidth = width * 32 + 148;
        var sceneHeight = height * 32 + 136;
        var horizontalCount = (int)Math.Ceiling(sceneWidth / 54.0) + 2;
        var verticalCount = (int)Math.Ceiling(sceneHeight / 58.0);
        for (var i = 0; i < horizontalCount; i++)
        {
            var x = -18 + i * (sceneWidth + 36.0) / Math.Max(1, horizontalCount - 1);
            foreach (var (seed, y, shadow) in new[] { (1200, 82.0, CandidateShadowKind.Foreground),
                (1300, sceneHeight + 20.0, CandidateShadowKind.FrameEdge) })
                result.Add(new(Trees[(int)Math.Floor(TileArtVariation.Hash(i, seed, 1) * Trees.Length)],
                    x + (TileArtVariation.Hash(i, seed, 2) - .5) * 18 - 74, y - 62,
                    .86 + TileArtVariation.Hash(i, seed, 3) * .2, TileArtVariation.Hash(i, seed, 4) > .5, 1, shadow));
        }
        for (var i = 1; i < verticalCount - 1; i++)
        {
            var y = i * (double)sceneHeight / Math.Max(1, verticalCount - 1);
            foreach (var (seed, x) in new[] { (1400, -26.0), (1500, width * 32 + 28.0) })
                result.Add(new(Trees[(int)Math.Floor(TileArtVariation.Hash(i, seed, 1) * Trees.Length)],
                    x, y + 32 - 62, .86 + TileArtVariation.Hash(i, seed, 3) * .18,
                    TileArtVariation.Hash(i, seed, 4) > .5, 1, CandidateShadowKind.FrameEdge));
        }
        result.Add(new("boulder-large", -5, 82, 1.05, false, .96, CandidateShadowKind.Foreground));
        result.Add(new("fallen-hollow-log", 92, sceneHeight + 4 - 62, .9, true, .96, CandidateShadowKind.Foreground));
        return result;
    }

    internal static int AddPerimeter(Node2D parent, EnvironmentCatalog environment, int width, int height,
        CandidateShadowCache shadows, List<string> diagnostics)
    {
        var count = 0;
        foreach (var placement in Perimeter(width, height))
        {
            try
            {
                var sprite = environment.CreateSprite(placement.Id, (float)placement.Scale, placement.Flip,
                    (float)placement.Opacity, shadows, placement.Shadow);
                sprite.Name = $"Perimeter_{count}";
                sprite.Position = new((float)placement.X, (float)placement.Y);
                parent.AddChild(sprite);
                count++;
            }
            catch (MissingArtResourceException error) { diagnostics.Add($"Perimeter {placement.Id}: {error.Message}"); }
        }
        return count;
    }

    internal static int AddCover(Node2D parent, JsonObject map, AssetCatalog assets,
        CandidateShadowCache shadows, List<string> diagnostics)
    {
        var covers = map["directionalCover"] as JsonArray
            ?? throw new InvalidOperationException("Canonical directional cover is absent.");
        var count = 0;
        foreach (var value in covers)
        {
            var cover = value!.AsObject();
            var sides = cover["sides"]!.AsObject();
            string? strongest = null;
            foreach (var side in sides)
            {
                var level = side.Value?.GetValue<string>();
                if (string.IsNullOrEmpty(level)) continue;
                if (level is not ("half" or "full")) throw new InvalidOperationException("Invalid canonical cover presentation level.");
                if (strongest != "full") strongest = level;
            }
            if (strongest is null) continue;
            var topic = strongest == "full" ? "C01-BPROP-COVER-2" : "C01-BPROP-COVER-1";
            try
            {
                var asset = assets.ResolveTopic(topic);
                var sprite = assets.CreateSpriteAtFrame(asset, 0);
                sprite.Name = $"Cover_{count}";
                var at = cover["at"]!;
                sprite.Position = new(at["x"]!.GetValue<int>() * 32 + 16, at["y"]!.GetValue<int>() * 32 + 31);
                sprite.AddChild(CandidateSpriteShadows.Create(sprite, CandidateShadowKind.BattleProp, 1 / sprite.Scale.X, shadows));
                sprite.SetMeta("cover_level", strongest);
                parent.AddChild(sprite);
                count++;
            }
            catch (MissingArtResourceException error) { diagnostics.Add($"Cover prop {topic}: {error.Message}"); }
        }
        return count;
    }
}
