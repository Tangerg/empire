#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class BattleScreenChecks
{
    private async Task CheckCaptureProgressFeedback()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-04-capture-feedback.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-04");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(),
            "The capture witness starts from the unchanged formal C04 opening.");
        var checkpoints = fixture["checkpoints"]!.AsArray().OfType<JsonObject>()
            .ToDictionary(value => value["index"]!.GetValue<int>());
        Require(checkpoints.Keys.Order().SequenceEqual(new[] { 46, 69, 87 }) && fixture["actions"]!.AsArray().Count == 88,
            "The fixture must retain both incomplete captures and the subsequent ownership change.");
        foreach (var (action, index) in fixture["actions"]!.AsArray().OfType<JsonObject>().Select((action, index) => (action, index)))
        {
            if (checkpoints.TryGetValue(index, out var checkpoint))
            {
                Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(),
                    "The original legal prefix must reach each exact partial/completed capture checkpoint.");
                var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
                await CheckCaptureOrder(session, level, engine, presentation, text, action, checkpoint);
            }
            battle.Dispatch(action);
            if (checkpoint is not null)
                Require(battle.StateHash == checkpoint["afterStateHash"]!.GetValue<string>(),
                    "The canonical replay and native capture must reach the same recorded state.");
        }
        CheckPartialCaptureDisclosure(presentation, battle.Snapshot());
    }

    private static void CheckPartialCaptureDisclosure(SessionPresentationText presentation, JsonObject state)
    {
        string PrivateIdentity() => throw new InvalidOperationException("Partial capture must not resolve an undisclosed participant identity.");
        var names = new BattleLogNames(_ => PrivateIdentity(), _ => PrivateIdentity(), _ => PrivateIdentity(), _ => PrivateIdentity(), _ => PrivateIdentity());
        var snapshot = EcmaJson.ToElement(state);
        var context = new BattleLogContext(names, snapshot, snapshot,
            new System.Collections.Generic.HashSet<int>(), new System.Collections.Generic.HashSet<int>());
        var value = new JsonObject
        {
            ["type"] = "capture",
            ["player"] = 999,
            ["at"] = new JsonObject { ["x"] = 7, ["y"] = 8 },
            ["progress"] = 0.425,
            ["captured"] = false
        };
        var original = EcmaJson.Stringify(value);
        Require(presentation.Events.Describe(context, EcmaJson.ToElement(value)) == "(7, 8) 占领进度 42.5%，尚未完成"
            && EcmaJson.Stringify(value) == original && EcmaJson.DeepEquals(state, EcmaJson.Parse(context.State.GetRawText())),
            "The partial message reads its event's site and fractional progress, not later map state or hidden participant names.");
    }

    private async Task CheckCaptureOrder(GrayFlagSession session, JsonObject level, BattleEngine engine,
        SessionPresentationText presentation, GameText text, JsonObject action, JsonObject checkpoint)
    {
        var before = session.Snapshot();
        var id = action["unit"]!.GetValue<int>();
        var actor = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == id);
        var origin = new BoardCell(actor["x"]!.GetValue<int>(), actor["y"]!.GetValue<int>());
        var destination = action["path"]!.AsArray()[^1]!.AsObject();
        var at = new BoardCell(destination["x"]!.GetValue<int>(), destination["y"]!.GetValue<int>());
        var index = at.Y * before["map"]!["width"]!.GetValue<int>() + at.X;
        var progress = checkpoint["progress"]!.GetValue<double>();
        var captured = checkpoint["captured"]!.GetValue<bool>();
        var expected = captured ? presentation.Battle.PlayerName("c01-04", 1) + " 占领了 (24, 15)"
            : progress == 0.42 ? "(24, 15) 占领进度 42%，尚未完成" : "(24, 15) 占领进度 84%，尚未完成";
        var originalSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1280, 720);
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AnimationsEnabled = false;
            var scene = AuthoredScene.Load("res://data/scenes/c01-04.json");
            var rendering = BoardSceneRendering.Load(scene, level, before["map"]!.AsObject());
            void Bind(GrayFlagSession value)
            {
                screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), value,
                    new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
                screen.ConfigureGround(rendering.Ground);
            }
            Bind(session);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(screen.Size.X >= 960 && screen.Size.Y >= 640, "The native feedback check uses a supported laid-out battle viewport.");
            var announced = new System.Collections.Generic.List<string>();
            screen.Board.EventPresented += (receipt, eventIndex) =>
            {
                if (!receipt.Events[eventIndex].GetProperty("type").ValueEquals("capture"))
                    return;
                var toast = (Label)screen.FindChild("BattleEventLog", true, false);
                Require(toast.IsVisibleInTree() && toast.Text == receipt.EventDescriptions[eventIndex]
                    && screen.RecentMessages.Last() == toast.Text,
                    "Capture feedback appears on its own committed event in the native toast and journal.");
                announced.Add(toast.Text);
            };
            GetViewport().GuiReleaseFocus();
            screen.SelectHit(TacticalHit.Unit(origin, id));
            void Press(Key key)
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
            }
            for (var step = 0; step < Math.Abs(at.X - origin.X); step++)
                Press(at.X > origin.X ? Key.Right : Key.Left);
            for (var step = 0; step < Math.Abs(at.Y - origin.Y); step++)
                Press(at.Y > origin.Y ? Key.Down : Key.Up);
            if (at != origin)
                Press(Key.Enter);
            Require(session.Revision == 0 && session.Events.Count == 0 && EcmaJson.DeepEquals(before, session.Snapshot())
                && !screen.RecentMessages.Contains(expected), "Selecting a destination cannot announce or commit capture progress.");
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var button = Descendants(hud).OfType<Button>().Single(value => value.GetMeta("hud_key", "").AsString() == "commands/capture");
            Require(button.IsVisibleInTree() && !button.Disabled, "The real capture order is available on the native HUD.");
            button.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var after = session.Snapshot();
            var events = new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray());
            Require(session.Revision == 1 && !screen.IsPresenting && session.ActionLog.Count == 1
                && EcmaJson.DeepEquals(session.ActionLog.Single(), action)
                && StateDigest.Hash(after) == checkpoint["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(events) == checkpoint["eventsSha256"]!.GetValue<string>(),
                "Native capture preserves the exact original command, state and ordered receipt.");
            var receipt = session.Events.Single(value => value["type"]!.GetValue<string>() == "capture");
            Require(receipt["progress"]!.GetValue<double>() == progress && receipt["captured"]!.GetValue<bool>() == captured
                && after["map"]!["owners"]![index]!.GetValue<int>() == (captured ? 1 : 2)
                && after["map"]!["captureProgress"]![index]!.GetValue<double>() == (captured ? 0 : progress * 100),
                "Partial capture retains the enemy owner; completion changes ownership and clears accumulated progress.");
            ((Button)screen.FindChild("BattleLogButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var journal = (RichTextLabel)screen.FindChild("BattleLogText", true, false);
            Require(((AcceptDialog)screen.FindChild("BattleLogDialog", true, false)).Visible && journal.IsVisibleInTree(),
                "The mounted journal is open for reading the completed capture receipt.");
            GD.Print($"CAPTURE_FEEDBACK progress={progress} captured={captured} description=[{string.Join(" | ", announced)}] state={StateDigest.Hash(after)}");
            Require(announced.SequenceEqual(new[] { expected }) && screen.RecentMessages.Count(line => line == expected) == 1
                && journal.Text.Split("\n\n", StringSplitOptions.None).Count(line => line == expected) == 1,
                "Partial capture must identify the site and committed progress, distinguish unfinished capture, and remain readable once in the journal.");
            ((AcceptDialog)screen.FindChild("BattleLogDialog", true, false)).Hide();
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            Require(announced.Count == 1 && screen.RecentMessages.SequenceEqual(messages),
                "Refreshing a settled capture must not repeat its feedback.");
            var restored = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), session.Save(), events);
            Require(EcmaJson.DeepEquals(after, restored.Snapshot()) && restored.GetSnapshot().Transition is null,
                "Save/load preserves capture facts without manufacturing a new receipt.");
            Bind(restored);
            screen.Refresh();
            Require(!screen.IsPresenting && !screen.RecentMessages.Contains(expected) && announced.Count == 1,
                "Rebinding a restored battle must not announce a historical capture again.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
            GetWindow().Size = originalSize;
        }
    }
}
