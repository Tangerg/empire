#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Presentation.Feedback;
using Empire.Localization;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>Resource ownership through actual session save/load, rebind, restart and queued teardown.</summary>
public partial class LightingLifetimeChecks : Node
{
    private int _checks;
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--construction-only", StringComparer.Ordinal))
            {
                FailedTerrainConstruction();
                GD.Print($"Lighting lifetime checks passed: {_checks} construction assertions.");
                GetTree().Quit();
                return;
            }
            StandaloneOwners();
            FailedTerrainConstruction();
            for (var cycle = 0; cycle < 3; cycle++)
                await ScreenLifetime(cycle);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"Lighting lifetime checks passed: {_checks} assertions, 3 real-session load/restart/scene-change cycles.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void StandaloneOwners()
    {
        for (var index = 0; index < 20; index++)
        {
            var contact = BoardLighting.CreateStructureContact(new(80, 96), .8f);
            var texture = contact.Texture;
            Require(GodotObject.IsInstanceValid(texture), "Standalone contact texture was not created.");
            contact.Free();
            Require(!GodotObject.IsInstanceValid(texture), "Standalone contact must explicitly dispose its managed texture wrapper before shutdown.");
        }
        var unit = BoardLighting.CreateUnitContact();
        var unitTexture = unit.Texture;
        unit.Free();
        Require(!GodotObject.IsInstanceValid(unitTexture), "Standalone unit contact lacks deterministic texture ownership.");
        var gauge = (Sprite2D)DynamicStateView.CreateGauge(40, 100);
        var gaugeTexture = gauge.Texture;
        gauge.Free();
        Require(!GodotObject.IsInstanceValid(gaugeTexture), "Replaced health gauges must release their exclusive procedural texture without GC.");
        var banner = DynamicStateView.CreateOwnerBanner("#aabbcc");
        var bannerTexture = banner.Texture;
        banner.Free();
        Require(!GodotObject.IsInstanceValid(bannerTexture), "Uncached owner banners must release their exclusive procedural texture without GC.");
        using var borrowedImage = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        using var borrowedTexture = ImageTexture.CreateFromImage(borrowedImage);
        var clipped = new Sprite2D { Texture = borrowedTexture };
        GroundClipper.Apply(clipped, borrowedTexture, new Vector2(2, 2));
        var clipMaterial = clipped.Material as ShaderMaterial ?? throw new InvalidOperationException("Clip material was not assigned.");
        var clipShader = clipMaterial.Shader;
        GroundClipper.Remove(clipped);
        Require(clipped.Material is null && !GodotObject.IsInstanceValid(clipMaterial)
            && !GodotObject.IsInstanceValid(clipShader), "Removing a ground clip must release its exclusive material and shader.");
        Require(GodotObject.IsInstanceValid(borrowedTexture), "Ground clipping must not dispose borrowed imported or board-owned textures.");
        GroundClipper.Apply(clipped, borrowedTexture, new Vector2(2, 2));
        var replacementMaterial = clipped.Material as ShaderMaterial ?? throw new InvalidOperationException("Replacement clip material was not assigned.");
        var replacementShader = replacementMaterial.Shader;
        clipped.Free();
        Require(!GodotObject.IsInstanceValid(replacementMaterial) && !GodotObject.IsInstanceValid(replacementShader),
            "Deleting a clipped sprite must release generated shader resources without GC.");
        Require(GodotObject.IsInstanceValid(borrowedTexture), "Sprite teardown must preserve borrowed texture ownership.");
        var materials = WorldMaterialsManifest.Load();
        var level = BattleTextChecks.OpeningLevel();
        var state = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json")).CreateBattle(level).Snapshot();
        var map = state["map"]!.AsObject();
        var lights = BoardLighting.CreateField(new(map["width"]!.GetValue<int>(), map["height"]!.GetValue<int>()),
            map["tiles"]!.AsArray().Select(EcmaJson.ReadString).ToArray(), materials);
        var textures = lights.Hearths.GetChildren().OfType<Sprite2D>().Select(sprite => sprite.Texture).Append(lights.Sunlight.Texture).ToArray();
        lights.Hearths.Free();
        lights.Sunlight.Free();
        Require(textures.All(texture => !GodotObject.IsInstanceValid(texture)), "Unshared field lights must release every texture wrapper deterministically.");
        using var cache = new ShadowCache();
        var first = BoardLighting.CreateStructureContact(new(80, 96), .8f, cache);
        var second = BoardLighting.CreateStructureContact(new(80, 96), .8f, cache);
        var shared = first.Texture;
        Require(ReferenceEquals(shared, second.Texture) && cache.TextureCount == 1, "Equal contacts must reuse one board-owned texture.");
        first.Free();
        Require(GodotObject.IsInstanceValid(shared), "A shared contact node must not dispose its board-owned texture.");
        second.Free();
        cache.Dispose();
        cache.Dispose();
        Require(cache.IsDisposed && cache.TextureCount == 0 && !GodotObject.IsInstanceValid(shared), "Board resource disposal must be complete and idempotent.");
        var rejected = false;
        try
        {
            BoardLighting.CreateStructureContact(new(80, 96), .8f, cache);
        }
        catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "A queued caller must not resurrect a released board resource owner.");
    }

    private void FailedTerrainConstruction()
    {
        using var source = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        using var borrowed = ImageTexture.CreateFromImage(source);
        OwnedProceduralSprite? failedSprite = null;
        Texture2D? failedTexture = null;
        ShaderMaterial? failedMaterial = null;
        Shader? failedShader = null;
        var failure = new InvalidOperationException("Deliberate configuration failure.");
        var propagated = false;
        try
        {
            OwnedProceduralSprite.Create(source, sprite =>
            {
                failedSprite = sprite;
                failedTexture = sprite.Texture;
                failedMaterial = OwnedShaderMaterial.Attach(sprite,
                    "shader_type canvas_item; uniform sampler2D borrowed_texture;").Value;
                failedShader = failedMaterial.Shader;
                failedMaterial.SetShaderParameter("borrowed_texture", borrowed);
                throw failure;
            });
        }
        catch (InvalidOperationException error) when (ReferenceEquals(error, failure))
        {
            propagated = true;
        }
        Require(propagated, "Construction must propagate the original configuration failure.");
        Require(failedSprite is not null && !GodotObject.IsInstanceValid(failedSprite),
            "Failed configuration must free its unattached procedural owner.");
        Require(failedTexture is not null && !GodotObject.IsInstanceValid(failedTexture)
            && failedMaterial is not null && !GodotObject.IsInstanceValid(failedMaterial)
            && failedShader is not null && !GodotObject.IsInstanceValid(failedShader),
            "Failed configuration must release exclusive texture, material and shader wrappers.");
        Require(GodotObject.IsInstanceValid(borrowed),
            "Failed configuration must preserve borrowed texture inputs.");
        var level = BattleTextChecks.OpeningLevel();
        var width = level["width"]!.GetValue<int>();
        var height = level["height"]!.GetValue<int>();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var tiles = engine.CreateState(level)["map"]!["tiles"]!.AsArray().Select(EcmaJson.ReadString).ToArray();
        foreach (var missing in new[] { false, true })
        {
            var materials = WorldMaterialsManifest.Load();
            var failureTiles = tiles.ToArray();
            failureTiles[0] = materials.TerrainTags.Single(pair => pair.Value.Contains("outside", StringComparer.Ordinal)).Key;
            var original = materials.Materials["field-material"];
            materials.Materials["field-material"] = new WorldMaterialRecord
            {
                Id = original.Id,
                SchemaVersion = missing ? original.SchemaVersion : -1,
                ResourcePath = missing ? "res://assets/missing-lifetime-fixture.png" : original.ResourcePath,
                LogicalSize = original.LogicalSize,
                RasterSize = original.RasterSize,
                WorldOrigin = original.WorldOrigin,
                Fit = original.Fit,
                Replaces = original.Replaces,
                Occupancy = original.Occupancy
            };
            var terrain = new TerrainView();
            try
            {
                terrain.Configure(materials, AuthoredScene.Load("res://data/scenes/c01-01.json"), EnvironmentCatalog.Load());
                if (missing)
                {
                    terrain.Render(failureTiles, new(width, height));
                    Require(terrain.Diagnostics.Any(message => message.Contains("missing-lifetime-fixture", StringComparison.Ordinal)),
                        "Missing material fixture must exercise the expected art diagnostic boundary.");
                }
                else
                {
                    var rejected = false;
                    try
                    {
                        terrain.Render(failureTiles, new(width, height));
                    }
                    catch (InvalidOperationException error) when (error.Message.StartsWith("Invalid world material geometry:", StringComparison.Ordinal))
                    {
                        rejected = true;
                    }
                    Require(rejected, "Malformed material geometry must fail before resource construction.");
                }
            }
            finally { terrain.Free(); }
        }
    }

    private async Task ScreenLifetime(int cycle)
    {
        var level = BattleTextChecks.OpeningLevel();
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var assets = AssetCatalog.Load();
        var environment = EnvironmentCatalog.Load();
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AdvanceAnimationsAutomatically = false;
        void Bind(GrayFlagSession session)
        {
            screen.BindSession(new(text, battleText, session, new(assets, environment, facing => facing == "west", null)), level, scene);
            screen.ConfigureDynamicState(engine.Catalog);
        }
        var session = new GrayFlagSession(new(engine, presentation), level);
        Bind(session);
        // Exercise the separate unshared field-light owners on one actual screen.
        if (cycle == 0)
            screen.ConfigureTerrain(WorldMaterialsManifest.Load());
        var cache = screen.Board.LightingResources;
        var contact = screen.Board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>()
            .Select(node => node.GetNodeOrNull<Sprite2D>("SourceStructureContact")).First(sprite => sprite is not null)!;
        var contactTexture = contact.Texture;
        var count = cache.TextureCount;
        for (var index = 0; index < 8; index++)
            screen.Board.RenderState(session.Snapshot());
        Require(cache.TextureCount == count, "Repeated canonical projections grew the lighting cache.");
        var replacementContact = screen.Board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>()
            .Select(node => node.GetNodeOrNull<Sprite2D>("SourceStructureContact")).First(sprite => sprite is not null)!;
        Require(ReferenceEquals(contactTexture, replacementContact.Texture), "Reprojection lost shared contact identity.");
        TacticalSceneInputChecks.Move(screen, session);
        Require(screen.Board.IsPresenting, "Real legal order did not start the cancellation fixture.");
        var loaded = GrayFlagSession.FromSave(new(engine, presentation), EcmaJson.Parse(EcmaJson.Stringify(session.Save()))!);
        Bind(loaded);
        Require(cache.IsDisposed && !GodotObject.IsInstanceValid(contactTexture), "Load/rebind did not release the old board resources.");
        var loadedCache = screen.Board.LightingResources;
        screen.Board.AdvancePresentation(100);
        Require(!screen.Board.IsPresenting && screen.DisplayedRevision == 0 && !loadedCache.IsDisposed,
            "Old playback callback resurrected/released the loaded view.");
        // Restart is a new canonical session, not a fake state rewind.
        var restarted = new GrayFlagSession(new(engine, presentation), level);
        Bind(restarted);
        Require(loadedCache.IsDisposed && !screen.Board.LightingResources.IsDisposed, "Restart did not replace exactly one resource owner.");
        screen.ConfigureTerrain(WorldMaterialsManifest.Load());
        var exclusiveTextures = Descendants(screen).OfType<OwnedProceduralSprite>().Select(sprite => sprite.Texture).ToArray();
        var generatedMaterials = Descendants(screen).OfType<OwnedShaderMaterial>().Select(owner => owner.Value).ToArray();
        Require(exclusiveTextures.Length > 0 && generatedMaterials.Length > 0,
            "The real board lifecycle must exercise procedural terrain and generated materials.");
        var exitingCache = screen.Board.LightingResources;
        var saves = 0;
        screen.BindApplicationActions(onSave: () => saves++);
        var staleSave = Buttons(screen).Single(button => button.Text == "保存");
        TacticalSceneInputChecks.Move(screen, restarted);
        Require(screen.Board.IsPresenting, "Restarted view did not start a live queued transition.");
        RemoveChild(screen); // The same unmount boundary used by application scene changes.
        staleSave.EmitSignal(Button.SignalName.Pressed);
        Require(saves == 0 && !screen.Board.IsPresenting, "Scene exit left stale application callbacks or playback active.");
        screen.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(exitingCache.IsDisposed && exitingCache.TextureCount == 0, "Queued scene teardown did not dispose its cache.");
        Require(exclusiveTextures.All(texture => !GodotObject.IsInstanceValid(texture)),
            "Queued board teardown retained exclusive procedural textures before GC.");
        Require(generatedMaterials.All(material => !GodotObject.IsInstanceValid(material)),
            "Queued board teardown retained exclusive generated materials before GC.");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"Lighting lifecycle cycle {cycle + 1}: load/rebind/restart/queued exit released resources.");
    }

    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node node) => node.GetChildren()
        .SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static System.Collections.Generic.IEnumerable<Button> Buttons(Node node) => node.GetChildren()
        .SelectMany(child => (child is Button button ? new[] { button } : Array.Empty<Button>()).Concat(Buttons(child)));
    private void Require(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
