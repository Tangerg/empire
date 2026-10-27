#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Empire.Presentation;

internal static class ActorFrameRegionChecks
{
    public static void Run()
    {
        var shipped = AssetCatalog.Load();
        var source = shipped.ResolveUnit("c01.swordsman");
        var uniform = shipped.ResolveEffect("C01-FX-01");
        Require(uniform.FrameRegions is null && uniform.FrameAnchors is null,
            "Legacy uniform geometry remains a distinct complete declaration.");
        for (var index = 0; index < uniform.FrameCount; index++)
            Require(shipped.CreateFrameTexture(uniform, index).Region == new Rect2(index * 64, 0, 64, 64),
                "Legacy uniform strips retain their original density-scaled physical regions.");
        var record = new JsonObject
        {
            ["topicId"] = "region-probe",
            ["category"] = "combat-unit",
            ["resourcePath"] = source.ResourcePath,
            ["physicalWidth"] = source.PhysicalWidth,
            ["physicalHeight"] = source.PhysicalHeight,
            ["pixelDensity"] = 1,
            ["presentationScale"] = .3,
            ["frames"] = 2,
            ["frameOrder"] = new JsonArray("idle", "attack"),
            ["frameRegions"] = JsonNode.Parse("[[0,0,20,24],[15,4,30,28]]"),
            ["frameAnchors"] = JsonNode.Parse("[[8,22],[19,27]]")
        };
        AssetCatalog Catalog(JsonObject value) => new(new AssetManifest
        {
            Schema = 2,
            LogicalTileSize = 16,
            Assets = new() { JsonSerializer.Deserialize<AssetRecord>(value.ToJsonString(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })! }
        }, new ArtBindings());
        var catalog = Catalog(record);
        var asset = catalog.ResolveTopic("region-probe");
        var sprite = catalog.CreateSprite(asset);
        Require(sprite.TextureFilter == CanvasItem.TextureFilterEnum.Nearest,
            "Explicit regions do not implicitly change authored pixel filtering.");
        var smoothRecord = (JsonObject)record.DeepClone();
        smoothRecord["textureFilter"] = "linear";
        var smoothCatalog = Catalog(smoothRecord);
        var smoothSprite = smoothCatalog.CreateSprite(smoothCatalog.ResolveTopic("region-probe"));
        try
        {
            Require(smoothSprite.TextureFilter == CanvasItem.TextureFilterEnum.Linear,
            "Hand-painted sheets explicitly opt in to linear filtering.");
        }
        finally { smoothSprite.Free(); }
        try
        {
            sprite.Position = new Vector2(17, 31);
            foreach (var facing in new[] { 1, -1 })
            {
                sprite.Scale = new Vector2(facing * .3f, .3f);
                for (var index = 0; index < 2; index++)
                {
                    catalog.ApplyFrame(sprite, asset, index);
                    var texture = (AtlasTexture)sprite.Texture!;
                    Require(texture.Region == (index == 0 ? new Rect2(0, 0, 20, 24) : new Rect2(15, 4, 30, 28)),
                        "Variable regions must retain their authored source coordinates and dimensions.");
                    Require(texture == catalog.CreateFrameTexture(asset, index), "Physical region cache must reuse its texture.");
                    Require(SpriteGroundContact.Offset(sprite).IsEqualApprox(Vector2.Zero)
                        && (sprite.Transform * (asset.AnchorAt(index) + sprite.Offset)).IsEqualApprox(sprite.Position),
                        "Frame changes and horizontal facing must preserve the authored foot at the parent contact.");
                }
            }
            var different = asset with
            {
                FrameRegions = new[] { new Rect2(1, 0, 20, 24), new Rect2(15, 4, 30, 28) }
            };
            Require(catalog.CreateFrameTexture(different, 0) != catalog.CreateFrameTexture(asset, 0),
                "Cache identity includes physical region, not only frame index.");
        }
        finally { sprite.Free(); }
        void Reject(Action<JsonObject> mutate)
        {
            var copy = (JsonObject)record.DeepClone();
            mutate(copy);
            try
            {
                _ = Catalog(copy);
            }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Malformed actor region contract was accepted: " + copy);
        }
        Reject(v => v["frameRegions"]![0] = JsonNode.Parse("[-1,0,20,24]"));
        Reject(v => v["frameRegions"]![0] = JsonNode.Parse("[0,0,0,24]"));
        Reject(v => v["frameRegions"]![0] = JsonNode.Parse($"[0,0,{source.PhysicalWidth + 1},24]"));
        Reject(v => v["frameRegions"]![0] = JsonNode.Parse($"[0,0,20,{source.PhysicalHeight + 1}]"));
        Reject(v => v["frameRegions"]![0] = JsonNode.Parse("[0,0,20]"));
        Reject(v => v["frameRegions"]!.AsArray().RemoveAt(1));
        Reject(v => v.Remove("frameAnchors"));
        Reject(v => v.Remove("frameRegions"));
        Reject(v => v["frameAnchors"]![1] = JsonNode.Parse("[31,27]"));
        Reject(v => v["frameAnchors"]!.AsArray().RemoveAt(1));
        Reject(v => v["pixelDensity"] = 2);
        Reject(v => v["textureFilter"] = "automatic");
        Reject(v => v["category"] = "fx");
        Reject(v => v["frameWidth"] = 20);
        Reject(v => v["frameHeight"] = 24);
        Reject(v => v["width"] = 20);
        Reject(v => v["height"] = 24);
        Reject(v => v["anchor"] = JsonNode.Parse("[8,22]"));
        Reject(v => v["groundFront"] = JsonNode.Parse("[8,22]"));
        GD.Print("Explicit actor frame region contracts passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
