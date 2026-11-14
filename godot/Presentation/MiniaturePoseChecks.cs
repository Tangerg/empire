#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class MiniaturePoseChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            CheckSampling();
            await CheckReceipt(false);
            await CheckReceipt(true);
            GD.Print("MINIATURE_POSE_OK authored attack/cast sampling, one real receipt timeline, settlement and cancellation.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void CheckSampling()
    {
        Require(MiniaturePoseAtlas.VisualTime(0, 150, 75, 360, 160) == 0, "Windup begins at zero.");
        Require(MiniaturePoseAtlas.VisualTime(75, 150, 75, 360, 160) == 160, "The owner's release is the visual release, not a second timer.");
        Require(MiniaturePoseAtlas.VisualTime(150, 150, 75, 360, 160) == 360, "Recovery finishes at the owner boundary.");
        var values = Enumerable.Range(0, 151).Select(ms => MiniaturePoseAtlas.VisualTime(ms, 150, 75, 360, 160)).ToArray();
        Require(values.Zip(values.Skip(1)).All(pair => pair.First <= pair.Second), "Pose progress cannot reverse.");
        var rejected = false;
        try
        {
            MiniaturePoseAtlas.VisualTime(-1, 150, 75, 360, 160);
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Require(rejected, "Invalid elapsed time must not become an idle fallback.");
        var atlas = new MiniaturePoseAtlas();
        var bindings = PresentationJson.Load<JsonObject>("res://assets/c01-miniature-roles/manifest.json");
        var actions = PresentationJson.Load<JsonObject>("res://assets/c01-miniature-roles/action-sheets.json");
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var assets = AssetCatalog.Load();
        var supported = 0;
        foreach (var unit in engine.CreateState(level)["units"]!.AsArray().OfType<JsonObject>())
        {
            var type = unit["type"]!.GetValue<string>();
            var role = bindings["unitRoles"]![type]!.GetValue<string>();
            var owner = assets.ResolveUnitClip(type, "attack", ActorClipUnit.Milliseconds, unit["key"]?.GetValue<string>());
            var sample = new ActorPoseSample(unit["id"]!.GetValue<int>(), "attack", 0, owner.Length, owner.ReleaseMs);
            var first = atlas.Sample(role, false, sample);
            if (actions["roles"]?[role] is null)
            {
                Require(first is null, "Unavailable cavalry/captain animations must not be silently substituted with another body.");
                continue;
            }
            var released = atlas.Sample(role, false, sample with
            {
                ElapsedMs = owner.RequireRelease()
            });
            var back = atlas.Sample(role, true, sample);
            Require(first is AtlasTexture a && released is AtlasTexture b && a.Region != b.Region
                && back is AtlasTexture c && a.Region != c.Region && a.Region.Size == new Vector2(64, 64),
                "Front/back and preparation/release must use distinct authored foot-anchored regions.");
            supported++;
        }
        Require(supported > 6, "Sample actual current roster, not only a single illustrative sprite.");
        Require(atlas.Sample("cleric", false, new(1, "cast", 300, 600, 300)) is not null,
            "Healing/casting and staff attack share the declared cleric visual, not duplicated frame data.");
        GD.Print($"Miniature pose atlas: {supported} current actors have explicit attack/cast visuals; unsupported roles stay explicit.");
    }

    private async Task CheckReceipt(bool cancel)
    {
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, text);
        var level = BattleTextChecks.OpeningLevel();
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(engine.CreateState(level)));
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
            var view = (MiniatureBoardView)screen.FindChild("MiniaturePresentation", true, false);
            var action = PresentationJson.Load<JsonObject>("res://tests/native/c01-steady-victory.json")["actions"]!.AsArray()[0]!.AsObject();
            var actorId = action["unit"]!.GetValue<int>();
            var samples = new List<ActorPoseSample>();
            var regions = new HashSet<Rect2>();
            screen.Board.ActorPosePresented += sample =>
            {
                if (sample.UnitId != actorId || sample.Action is not ("attack" or "cast"))
                    return;
                samples.Add(sample);
                var sprite = view.GetNode<Sprite3D>($"MiniatureViewport/MiniatureWorld/Actors/Unit_{actorId}");
                if (sprite.Texture is AtlasTexture frame)
                    regions.Add(frame.Region);
            };
            Require(session.Dispatch(EcmaJson.ToElement(action), session.Revision).Accepted, "Use the actual first recorded C01 player attack.");
            var committed = EcmaJson.Stringify(session.Snapshot());
            screen.Refresh();
            if (cancel)
                screen.Board.CancelPresentation();
            for (var frame = 0; screen.IsPresenting && frame < 1200; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!screen.IsPresenting, "The original receipt presenter must settle or cancel without an independent 3D waiter.");
            Require(session.Revision == 1 && session.ActionLog.Count == 1 && committed == EcmaJson.Stringify(session.Snapshot()),
                "Visual pose samples must not dispatch or mutate the committed result.");
            if (!cancel)
                Require(samples.Count > 1 && regions.Count > 1 && samples.All(sample => sample.ReleaseMs == 320)
                    && samples.Zip(samples.Skip(1)).All(pair => pair.First.ElapsedMs <= pair.Second.ElapsedMs),
                    "The actual archer receipt must drive multiple 3D frames from its sole 320ms release clock.");
            var settledCount = samples.Count;
            for (var frame = 0; frame < 3; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(samples.Count == settledCount, "No pose callbacks may survive the finished/cancelled receipt.");
            GD.Print($"Miniature real receipt cancel={cancel}: {samples.Count} pose samples, {regions.Count} actual atlas regions, one revision.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
