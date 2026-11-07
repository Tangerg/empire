#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

// Opt-in isolated experiment. Existing walking/attack poses are timing markers, not approved hit art.
public partial class HitDiagnosticChecks : Node
{
    private BoardView board = null!;
    private readonly List<object> samples = new();
    private readonly List<int> presented = new();
    private bool recording;
    private double elapsed;
    private double lastDelta;
    private ulong start;
    private string output = "";
    private int target;
    private int captures;
    private bool capturing;

    public override async void _Ready()
    {
        try
        {
            output = OS.GetEnvironment("EMPIRE_HIT_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new Exception("EMPIRE_HIT_OUTPUT is required.");
            Directory.CreateDirectory(output);
            var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-04-facing.json");
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == "c01-04");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var battle = engine.CreateBattle(level, fixture["seed"]!.GetValue<uint>());
            Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Initial hash");
            var prefix = fixture["prefixActions"]!.AsArray();
            for (int i = 0; i < prefix.Count; i++)
            {
                battle.Dispatch(prefix[i]!.AsObject());
                Require(battle.StateHash == fixture["prefixStateHashes"]![i]!.GetValue<string>(), "Prefix hash " + i);
            }
            Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(), "Before hash");
            var action = fixture["action"]!.AsObject();
            var receipt = battle.Dispatch(action);
            var before = receipt.Before;
            var after = battle.Snapshot();
            var events = receipt.Events;
            Require(battle.StateHash == fixture["afterStateHash"]!.GetValue<string>(), "After hash");
            Require(EcmaJson.Sha256(new JsonArray(events.Select(x => (JsonNode)x.DeepClone()).ToArray())) == fixture["eventsSha256"]!.GetValue<string>(), "Events hash");
            var hit = events.First(e => e["defender"] is not null && (e["damage"]?.GetValue<double>() ?? 0) > 0);
            target = hit["defender"]!.GetValue<int>();
            var manifest = PresentationJson.Load<AssetManifest>("res://data/selected-assets.json");
            var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
            if (OS.GetEnvironment("EMPIRE_HIT_CANDIDATE") == "1")
            {
                manifest.ActorClips["experiment.hit-markers"] = new()
                {
                    Unit = "milliseconds",
                    Frames = new[]
                    {
                        new ActorClipFrameRecord { Frame = "walk-a", Span = 50 },
                        new ActorClipFrameRecord { Frame = "attack", Span = 50 },
                        new ActorClipFrameRecord { Frame = "walk-b", Span = 50 }
                    }
                };
                var targetType = before["units"]!.AsArray().OfType<JsonObject>().Single(u => u["id"]!.GetValue<int>() == target)["type"]!.GetValue<string>();
                bindings.Units[targetType].ActionClips["hit"] = "experiment.hit-markers";
                foreach (var item in bindings.Actors.Values.Where(a => a.ExpectedUnitType == targetType))
                    item.UnitArt.ActionClips["hit"] = "experiment.hit-markers";
            }
            var scene = AuthoredScene.Load("res://data/scenes/c01-04.json");
            var rendering = BoardSceneRendering.Load(scene, level, engine.CreateState(level)["map"]!.AsObject());
            board = new BoardView { AdvanceAnimationsAutomatically = false };
            AddChild(board);
            board.Configure(new(new AssetCatalog(manifest, bindings), EnvironmentCatalog.Load(),
                engine.Grids.Get(before["rules"]!["grid"]!.GetValue<string>()), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.ConfigureDynamicState();
            board.RenderState(before);
            var actor = board.GetNode<Node2D>($"CanonicalProjection/Unit_{target}");
            board.Position = new Vector2(640, 360) - actor.Position;
            var feedback = new Control { Size = new Vector2(1280, 720) };
            AddChild(feedback);
            board.EventPresented += (_, index) => presented.Add(index);
            var headless = DisplayServer.GetName() == "headless";
            if (!headless)
            {
                RenderingServer.FramePostDraw += AfterDraw;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            else
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            start = Time.GetTicksUsec();
            recording = true;
            var transition = new BattlePresentationTransition(1, EcmaJson.ToElement(before), EcmaJson.ToElement(action),
                events.Select(EcmaJson.ToElement).ToArray(), EcmaJson.ToElement(after), false, null,
                events.Select(_ => "Diagnostic receipt").ToArray());
            var task = board.PresentTransitionAsync(feedback, transition);
            Sample("start");
            while (!task.IsCompleted && elapsed < 10)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(task.IsCompleted && await task, "Playback completion");
            if (!headless)
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            recording = false;
            if (!headless)
                RenderingServer.FramePostDraw -= AfterDraw;
            Require(presented.SequenceEqual(Enumerable.Range(0, events.Count)), "Event order");
            Require(StateDigest.Hash(before) == fixture["beforeStateHash"]!.GetValue<string>() && StateDigest.Hash(after) == fixture["afterStateHash"]!.GetValue<string>(), "Unmodified state");
            File.WriteAllText(Path.Combine(output, "trace.json"), JsonSerializer.Serialize(new
            {
                experiment = "new existing-pose timing fixture; not restored or accepted hit art",
                displayDriver = DisplayServer.GetName(),
                mode = OS.GetEnvironment("EMPIRE_HIT_MODE"),
                target,
                targetType = before["units"]!.AsArray().OfType<JsonObject>().Single(u => u["id"]!.GetValue<int>() == target)["type"]!.GetValue<string>(),
                candidate = OS.GetEnvironment("EMPIRE_HIT_CANDIDATE"),
                captures,
                beforeHash = fixture["beforeStateHash"]!.GetValue<string>(),
                afterHash = battle.StateHash,
                eventsHash = fixture["eventsSha256"]!.GetValue<string>(),
                presented,
                samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("Hit diagnostic passed; recorded " + samples.Count + " samples.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    public override void _Process(double delta)
    {
        if (!recording)
            return;
        lastDelta = delta;
        elapsed += delta;
        board.AdvancePresentation(delta);
        Sample("process");
    }

    private void Sample(string stage)
    {
        var actor = board.GetNodeOrNull<Node2D>($"CanonicalProjection/Unit_{target}");
        var sprite = actor?.GetNodeOrNull<Sprite2D>("AuthoredSprite");
        samples.Add(new
        {
            stage,
            elapsedMs = elapsed * 1000,
            deltaMs = lastDelta * 1000,
            wallMs = (Time.GetTicksUsec() - start) / 1000.0,
            processFrame = Engine.GetProcessFrames(),
            drawnFrame = Engine.GetFramesDrawn(),
            frame = sprite?.GetMeta("frame").AsInt32(),
            hit = board.GetNodeOrNull("BattleEffects/Hit") is not null,
            events = presented.Count
        });
    }

    private void AfterDraw()
    {
        if (!recording || capturing)
            return;
        Sample("post_draw");
        if (OS.GetEnvironment("EMPIRE_HIT_CAPTURE") != "1")
            return;
        capturing = true;
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(Path.Combine(output, $"viewport-{captures++:D4}.png"));
        capturing = false;
    }

    private static void Require(bool value, string label)
    {
        if (!value)
            throw new Exception(label + " failed.");
    }
}
