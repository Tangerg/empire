#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
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
            StandaloneOwners();
            for (var cycle = 0; cycle < 3; cycle++) await ScreenLifetime(cycle);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
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
            var contact = CandidateBoardLighting.CreateStructureContact(new(80, 96), .8f);
            var texture = contact.Texture;
            Require(GodotObject.IsInstanceValid(texture), "Standalone contact texture was not created.");
            contact.Free();
            Require(!GodotObject.IsInstanceValid(texture), "Standalone contact must explicitly dispose its managed texture wrapper before shutdown.");
        }
        var unit = CandidateBoardLighting.CreateUnitContact(); var unitTexture = unit.Texture;
        unit.Free();
        Require(!GodotObject.IsInstanceValid(unitTexture), "Standalone unit contact lacks deterministic texture ownership.");
        var materials = WorldMaterialsManifest.Load();
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var state = new GrayFlagEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json")).CreateBattle(level).Snapshot();
        var map = state["map"]!.AsObject();
        var lights = CandidateBoardLighting.CreateField(new(map["width"]!.GetValue<int>(), map["height"]!.GetValue<int>()),
            map["tiles"]!.AsArray().Select(EcmaJson.ReadString).ToArray(), materials);
        var textures = lights.Hearths.GetChildren().OfType<Sprite2D>().Select(sprite => sprite.Texture).Append(lights.Sunlight.Texture).ToArray();
        lights.Hearths.Free(); lights.Sunlight.Free();
        Require(textures.All(texture => !GodotObject.IsInstanceValid(texture)), "Unshared field lights must release every texture wrapper deterministically.");
        using var cache = new CandidateShadowCache();
        var first = CandidateBoardLighting.CreateStructureContact(new(80, 96), .8f, cache);
        var second = CandidateBoardLighting.CreateStructureContact(new(80, 96), .8f, cache);
        var shared = first.Texture;
        Require(ReferenceEquals(shared, second.Texture) && cache.TextureCount == 1, "Equal contacts must reuse one board-owned texture.");
        first.Free();
        Require(GodotObject.IsInstanceValid(shared), "A shared contact node must not dispose its board-owned texture.");
        second.Free(); cache.Dispose(); cache.Dispose();
        Require(cache.IsDisposed && cache.TextureCount == 0 && !GodotObject.IsInstanceValid(shared), "Board resource disposal must be complete and idempotent.");
        var rejected = false;
        try { CandidateBoardLighting.CreateStructureContact(new(80, 96), .8f, cache); }
        catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "A queued caller must not resurrect a released board resource owner.");
    }

    private async Task ScreenLifetime(int cycle)
    {
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new GrayFlagEngine(packs);
        var assets = AssetCatalog.Load(); var environment = EnvironmentCatalog.Load(); var scene = AuthoredScene.Load();
        var screen = new BattleScreen(); AddChild(screen);
        screen.Board.AdvanceAnimationsAutomatically = false;
        void Bind(GrayFlagSession session)
        {
            screen.BindSession(session, assets, level, scene, environment, facing => facing == "west");
            screen.ConfigureDynamicState(packs);
        }
        var session = new GrayFlagSession(engine, level); Bind(session);
        // Exercise the separate unshared field-light owners on one actual screen.
        if (cycle == 0) screen.ConfigureTerrain(WorldMaterialsManifest.Load());
        var cache = screen.Board.LightingResources;
        var contact = screen.Board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>()
            .Select(node => node.GetNodeOrNull<Sprite2D>("SourceStructureContact")).First(sprite => sprite is not null)!;
        var contactTexture = contact.Texture; var count = cache.TextureCount;
        for (var index = 0; index < 8; index++) screen.Board.RenderState(session.Snapshot());
        Require(cache.TextureCount == count, "Repeated canonical projections grew the lighting cache.");
        var replacementContact = screen.Board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>()
            .Select(node => node.GetNodeOrNull<Sprite2D>("SourceStructureContact")).First(sprite => sprite is not null)!;
        Require(ReferenceEquals(contactTexture, replacementContact.Texture), "Reprojection lost shared contact identity.");
        var unit = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == 2);
        var origin = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
        var order = session.GetLegalOrders(2).First(choice => choice.PresentationRole == "move" && choice.Target is { } at && at != origin);
        screen.SubmitOrder(order);
        Require(screen.Board.IsPresenting, "Real legal order did not start the cancellation fixture.");
        var loaded = GrayFlagSession.FromSave(engine, EcmaJson.Parse(EcmaJson.Stringify(session.Save()))!);
        Bind(loaded);
        Require(cache.IsDisposed && !GodotObject.IsInstanceValid(contactTexture), "Load/rebind did not release the old board resources.");
        var loadedCache = screen.Board.LightingResources;
        screen.Board.AdvancePresentation(100);
        Require(!screen.Board.IsPresenting && screen.DisplayedRevision == 0 && !loadedCache.IsDisposed,
            "Old playback callback resurrected/released the loaded view.");
        // Restart is a new canonical session, not a fake state rewind.
        Bind(new GrayFlagSession(engine, level));
        Require(loadedCache.IsDisposed && !screen.Board.LightingResources.IsDisposed, "Restart did not replace exactly one resource owner.");
        var exitingCache = screen.Board.LightingResources;
        var saves = 0; screen.BindApplicationActions(onSave: () => saves++);
        var staleSave = Buttons(screen).Single(button => button.Text == "保存");
        screen.SubmitOrder(order);
        Require(screen.Board.IsPresenting, "Restarted view did not start a live queued transition.");
        RemoveChild(screen); // The same unmount boundary used by application scene changes.
        staleSave.EmitSignal(Button.SignalName.Pressed);
        Require(saves == 0 && !screen.Board.IsPresenting, "Scene exit left stale application callbacks or playback active.");
        screen.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(exitingCache.IsDisposed && exitingCache.TextureCount == 0, "Queued scene teardown did not dispose its cache.");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"Lighting lifecycle cycle {cycle + 1}: load/rebind/restart/queued exit released resources.");
    }

    private static System.Collections.Generic.IEnumerable<Button> Buttons(Node node) => node.GetChildren()
        .SelectMany(child => (child is Button button ? new[] { button } : Array.Empty<Button>()).Concat(Buttons(child)));
    private void Require(bool condition, string message) { _checks++; if (!condition) throw new InvalidOperationException(message); }
}
