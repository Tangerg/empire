#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using System.Text.Json;
using Godot;

namespace Empire.Presentation;

public partial class FigureGeometryChecks : Node
{
    public override void _Ready()
    {
        try
        {
            var directory = OS.GetCmdlineUserArgs()[0];
            DirAccess.MakeDirRecursiveAbsolute(directory);
            var assets = AssetCatalog.Load();
            var asset = assets.ResolveUnit("c01.swordsman");
            var result = new List<object>();
            using var cache = new ShadowCache();
            for (var index = 0; index < asset.FrameCount; index++)
            {
                var sprite = assets.CreateSpriteAtFrame(asset, index);
                AddChild(sprite);
                var figure = SpriteShadows.CreateFigure(sprite, Colors.White, cache);
                sprite.AddChild(figure);
                using var source = SpriteShadows.ReadFrame(sprite.Texture);
                using var output = figure.Texture.GetImage();
                var sourceBounds = AlphaBounds(source);
                var outputBounds = AlphaBounds(output);
                var scale = sprite.Scale.Abs();
                var solo = LayerFilterComposition.Compose(new[] {
                    new LayerFilterComposition.Layer(source, new(sprite.Offset * scale, sprite.Texture.GetSize() * scale),
                        AreaMinification: true)
                }, 2);
                using (solo.Image)
                {
                    var soloBounds = AlphaBounds(solo.Image);
                    var expected = new Vector2(sourceBounds.Size.X, sourceBounds.Size.Y) * scale;
                    var actual = new Vector2(soloBounds.Size.X, soloBounds.Size.Y) / 2;
                    if (Mathf.Abs(expected.X - actual.X) > 1.5f || Mathf.Abs(expected.Y - actual.Y) > 1.5f)
                        throw new InvalidOperationException($"Figure body shape changed: {expected} vs {actual}.");
                    var global = figure.GlobalTransform;
                    if (!Mathf.IsEqualApprox(global.X.Length(), global.Y.Length()))
                        throw new InvalidOperationException("Figure final axes must have equal scale.");
                    source.SavePng(directory + $"/swordsman-{index}-source.png");
                    output.SavePng(directory + $"/swordsman-{index}-figure.png");
                    solo.Image.SavePng(directory + $"/swordsman-{index}-body-only.png");
                    result.Add(new
                    {
                        frame = index,
                        role = asset.FrameOrder[index],
                        sheet = asset.ResourcePath,
                        atlasRegion = R(asset.RegionAt(index)),
                        sourceAlpha = R(sourceBounds),
                        sourceScale = V(sprite.Scale),
                        sourceOffset = V(sprite.Offset),
                        textureSize = V(sprite.Texture.GetSize()),
                        cpuBounds = R(new Rect2(sprite.Offset * scale, sprite.Texture.GetSize() * scale)),
                        bodyOutputAlpha = R(soloBounds),
                        bodyExpectedWorldSize = V(expected),
                        bodyActualWorldSize = V(actual),
                        figureTextureSize = V(figure.Texture.GetSize()),
                        figureOutputAlpha = R(outputBounds),
                        figureOffset = V(figure.Offset),
                        figureScale = V(figure.Scale),
                        figureGlobalAxisLengths = new[] { global.X.Length(), global.Y.Length() },
                        figureAlphaWorldSize = V(new Vector2(outputBounds.Size.X * global.X.Length(), outputBounds.Size.Y * global.Y.Length()))
                    });
                }
                sprite.Free();
            }
            if (OS.GetCmdlineUserArgs().Length > 1)
                CheckSavedBoard(directory, OS.GetCmdlineUserArgs()[1]);
            System.IO.File.WriteAllText(directory + "/geometry.json", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("Figure geometry checks passed: real atlas/body/composition transforms preserve both axes.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void CheckSavedBoard(string directory, string savePath)
    {
        var saved = JsonNode.Parse(System.IO.File.ReadAllText(savePath))!.AsObject();
        var state = saved["battle"]!["state"]!.AsObject();
        var original = state.ToJsonString();
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-07");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var scene = AuthoredScene.Load("res://data/scenes/c01-07.json");
        var art = PresentationJson.Load<PaintedGroundDocument>(scene.PaintedGroundPath!);
        var board = new BoardView { Scale = Vector2.One * 1.25f };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane), level, scene);
            board.ConfigureGround(art.Factory(state["map"]!.AsObject()));
            board.ConfigureDynamicState();
            board.RenderState(state);
            var actor = (Node2D)board.FindChild("Unit_9", true, false)!;
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            var figure = sprite.GetNode<SpriteShadows>("SourceSpriteShadows");
            var chain = new List<object>();
            for (Node? node = figure; node is not null && node != this; node = node.GetParent())
                if (node is Node2D item)
                {
                    var t = item.GlobalTransform;
                    chain.Add(new
                    {
                        name = item.Name.ToString(),
                        type = item.GetType().Name,
                        scale = V(item.Scale),
                        globalAxes = new[] { t.X.Length(), t.Y.Length() }
                    });
                    if (!Mathf.IsEqualApprox(t.X.Length(), t.Y.Length()))
                        throw new InvalidOperationException("Real T7 ancestor has anisotropic scale: " + item.Name);
                }
            using var image = figure.Texture.GetImage();
            image.SavePng(directory + "/t7-unit9-actual-figure.png");
            System.IO.File.WriteAllText(directory + "/t7-unit9-transforms.json", JsonSerializer.Serialize(chain,
                new JsonSerializerOptions { WriteIndented = true }));
            if (state.ToJsonString() != original)
                throw new InvalidOperationException("Read-only render changed saved state.");
        }
        finally { board.Free(); }
    }

    private static float[] V(Vector2 value) => new[] { value.X, value.Y };
    private static float[] R(Rect2 value) => new[] { value.Position.X, value.Position.Y, value.Size.X, value.Size.Y };
    private static Rect2 AlphaBounds(Image image)
    {
        var left = image.GetWidth();
        var top = image.GetHeight();
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < image.GetHeight(); y++)
            for (var x = 0; x < image.GetWidth(); x++)
                if (image.GetPixel(x, y).A >= .05f)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
        return new Rect2(left, top, right - left + 1, bottom - top + 1);
    }
}
