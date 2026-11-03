#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

/// <summary>Committed owner-turn recovery through real sessions, mounted input and controlled native playback.</summary>
public partial class RegenerationFeedbackChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            await CheckRecordedRecovery("c01-01-regeneration.json");
            await CheckRecordedRecovery("c01-04-regeneration.json");
            await CheckRecordedRecovery("c01-08-regeneration.json");
            GD.Print("Regeneration feedback native checks passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task CheckRecordedRecovery(string filename)
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/" + filename);
        var levelId = fixture["levelId"]!.GetValue<string>();
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == levelId);
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Recovery witness starts from unchanged formal content.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(), "The unchanged legal prefix reaches the exact recorded recovery state.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load($"res://data/scenes/{levelId}.json"));
            GetViewport().GuiReleaseFocus();
            var completed = new List<int>();
            screen.Board.EventPresented += (_, index) => completed.Add(index);
            Require(session.IsComputerTurn == fixture["isComputerAction"]!.GetValue<bool>(), "The witness preserves its actual human or AI controller.");
            if (session.IsComputerTurn)
            {
                Require(session.AdvanceComputer(), "The real AI session must advance its exhausted turn.");
                screen.Refresh();
            }
            else
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
            var transition = session.GetSnapshot().Transition!;
            Require(session.Revision == 1 && EcmaJson.DeepEquals(session.ActionLog.Single(), fixture["action"])
                && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()))
                    == fixture["eventsSha256"]!.GetValue<string>(),
                "Feedback must preserve the exact recorded command, health transitions and ordered events.");
            var indices = transition.Events.Select((value, index) => (value, index))
                .Where(pair => pair.value.GetProperty("type").ValueEquals("regen")).Select(pair => pair.index).ToArray();
            var expected = indices.Select(index =>
            {
                var value = transition.Events[index];
                var unit = transition.After.GetProperty("units").EnumerateArray()
                    .Single(unit => unit.GetProperty("id").GetInt32() == value.GetProperty("unit").GetInt32());
                return session.UnitName(unit) + "：回合恢复 " + value.GetProperty("amount").GetDouble().ToString(CultureInfo.InvariantCulture) + " 点生命";
            }).ToArray();
            var floating = await ObserveRecovery(screen.Board);
            Require(!screen.IsPresenting && screen.Board.GetNodeOrNull("BattleEffects") is null
                && completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count)),
                "Each committed event completes once, in order, and releases its effects.");
            var described = indices.Select(index => transition.EventDescriptions[index]).ToArray();
            GD.Print($"REGEN_FEEDBACK {levelId}: descriptions=[{string.Join(" | ", described)}]; floating=[{string.Join(",", floating.Select(effect => effect.Number))}]; state={StateDigest.Hash(session.Snapshot())}");
            Require(described.SequenceEqual(expected)
                && floating.Select(effect => effect.Number).SequenceEqual(indices.Select(index => "+" + transition.Events[index].GetProperty("amount").GetDouble().ToString(CultureInfo.InvariantCulture)))
                && expected.All(line => screen.RecentMessages.Contains(line)),
                "Visible committed regeneration must show its exact actual amount in both healing feedback and the journal.");
            Require(floating.Select(effect => effect.Position).SequenceEqual(indices.Select(index =>
            {
                var unit = transition.After.GetProperty("units").EnumerateArray()
                    .Single(unit => unit.GetProperty("id").GetInt32() == transition.Events[index].GetProperty("unit").GetInt32());
                return screen.Board.Projection.CellCenter(new(unit.GetProperty("x").GetInt32(), unit.GetProperty("y").GetInt32()));
            })), "Recovery effects use the visible settled unit positions, not an invented event location.");
            var messages = screen.RecentMessages.ToArray();
            var eventCount = completed.Count;
            screen.Refresh();
            screen.Refresh();
            Require(!screen.IsPresenting && completed.Count == eventCount && screen.RecentMessages.SequenceEqual(messages)
                && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>(),
                "Refreshing a settled revision cannot repeat recovery or change health.");
            if (levelId == "c01-01")
                await CheckVisibilityAndLifetime(screen.Board, session, presentation, engine, level, transition, indices.Single());
            if (fixture["hiddenBoundary"] is JsonObject hiddenBoundary)
            {
                foreach (var action in hiddenBoundary["continuation"]!.AsArray().OfType<JsonObject>())
                    battle.Dispatch(action);
                Require(battle.StateHash == hiddenBoundary["beforeStateHash"]!.GetValue<string>(),
                    "The isolated fog boundary starts from another unchanged legal recorded prefix.");
                var fogState = battle.Snapshot();
                fogState["rules"]!["fog"] = true;
                Require(StateDigest.Hash(fogState) == hiddenBoundary["fogBeforeStateHash"]!.GetValue<string>(),
                    "This explicit visibility fixture changes only the fog flag, not unit positions, health or commands.");
                var hiddenSession = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(fogState));
                var hiddenId = hiddenBoundary["unit"]!.GetValue<int>();
                Require(!hiddenSession.IsComputerTurn && hiddenSession.GetSnapshot().VisibleUnitIds is { } beforeVisible && !beforeVisible.Contains(hiddenId),
                    "The real session visibility query must actually conceal the regenerating enemy before the human action.");
                screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), hiddenSession,
                    new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                    level, AuthoredScene.Load($"res://data/scenes/{levelId}.json"));
                GetViewport().GuiReleaseFocus();
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
                var hiddenAfter = hiddenSession.GetSnapshot();
                Require(hiddenSession.Revision == 1 && StateDigest.Hash(hiddenSession.Snapshot()) == hiddenBoundary["fogAfterStateHash"]!.GetValue<string>()
                    && EcmaJson.DeepEquals(hiddenSession.ActionLog.Single(), hiddenBoundary["action"])
                    && hiddenAfter.VisibleUnitIds is { } afterVisible && !afterVisible.Contains(hiddenId),
                    "The real hidden recovery still commits normally and remains outside the player's final vision.");
                var hiddenReceipt = hiddenAfter.Transition!;
                var hiddenIndex = hiddenReceipt.Events.Select((value, index) => (value, index))
                    .Single(pair => pair.value.GetProperty("type").ValueEquals("regen") && pair.value.GetProperty("unit").GetInt32() == hiddenId).index;
                Require(hiddenReceipt.Events[hiddenIndex].GetProperty("amount").GetDouble() == hiddenBoundary["amount"]!.GetValue<double>()
                    && hiddenReceipt.EventDescriptions[hiddenIndex].Length == 0 && (await ObserveRecovery(screen.Board)).Count == 0,
                    "Application visibility injection must suppress hidden recovery prose and effects without dropping its actual event.");
                GD.Print($"REGEN_HIDDEN_SESSION {levelId}: unit={hiddenId}; amount=20; description empty; no effect; state={StateDigest.Hash(hiddenSession.Snapshot())}");
            }
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private async Task<IReadOnlyList<(string Number, Vector2 Position)>> ObserveRecovery(BoardView board)
    {
        var floating = new List<(string, Vector2)>();
        var effects = new HashSet<ulong>();
        for (var tick = 0; board.IsPresenting && tick < 200; tick++)
        {
            if (board.GetNodeOrNull<Node2D>("BattleEffects/Heal") is { } effect && effects.Add(effect.GetInstanceId()))
            {
                floating.Add((effect.GetNode<Label>("Number").Text, effect.Position));
                Require(effect.IsVisibleInTree() && effect.GetNode<Sprite2D>("Strip").GetMeta("topic_id").AsString() == "C01-FX-17",
                    "Owner-turn recovery reuses the visible injected production healing art.");
            }
            board.AdvancePresentation(0.05);
            await Task.Yield();
        }
        Require(!board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null, "Bounded recovery playback must settle and release its effects.");
        return floating;
    }

    private async Task CheckVisibilityAndLifetime(BoardView board, GrayFlagSession session, SessionPresentationText presentation,
        BattleEngine engine, JsonObject level, BattlePresentationTransition original, int regenerationIndex)
    {
        var id = original.Events[regenerationIndex].GetProperty("unit").GetInt32();
        var levelId = level["id"]!.GetValue<string>();
        var names = new BattleLogNames(session.UnitName, presentation.Content.UnitName, presentation.Content.CareerName,
            presentation.Content.ResourceName, player => presentation.Battle.PlayerName(levelId, player));
        var visible = new HashSet<int> { id };
        var hidden = new HashSet<int>();
        var cases = new (IReadOnlySet<int>? Before, IReadOnlySet<int>? After, bool Expected)[]
        {
            (null, hidden, false), (hidden, null, false), (hidden, hidden, false),
            (hidden, visible, false), (visible, hidden, false), (visible, visible, true)
        };
        foreach (var item in cases)
        {
            var context = new BattleLogContext(names, original.Before, original.After, item.Before, item.After);
            var descriptions = presentation.Events.DescribeAll(context, original.Events);
            Require(descriptions.Count == original.Events.Count && (descriptions[regenerationIndex].Length > 0) == item.Expected,
                "Explicit visibility-boundary fixtures must preserve event slots while hiding recovery prose outside both visible boundaries.");
            var transition = new BattlePresentationTransition(original.Revision, original.Before, original.Action, original.Events,
                original.After, original.IsComputerAction, item.Before, descriptions);
            var playback = board.PresentTransitionAsync(transition, item.After);
            var floating = await ObserveRecovery(board);
            Require(await playback && floating.Count == (item.Expected ? 1 : 0),
                "The exact same visibility policy must suppress hidden, newly revealed and newly concealed recovery effects.");
        }
        var mutableBefore = new HashSet<int> { id };
        var mutableAfter = new HashSet<int> { id };
        var frozen = new BattleLogContext(names, original.Before, original.After, mutableBefore, mutableAfter);
        mutableBefore.Clear();
        mutableAfter.Clear();
        Require(presentation.Events.DescribeAll(frozen, original.Events)[regenerationIndex] == original.EventDescriptions[regenerationIndex],
            "Later caller mutations cannot change the receipt's projected visibility.");

        foreach (var initiallyVisible in new[] { true, false })
        {
            var afterAtEntry = new HashSet<int>(initiallyVisible ? new[] { id } : Array.Empty<int>());
            var descriptions = presentation.Events.DescribeAll(new BattleLogContext(names, original.Before, original.After, visible, afterAtEntry), original.Events);
            var transition = new BattlePresentationTransition(original.Revision, original.Before, original.Action, original.Events,
                original.After, original.IsComputerAction, visible, descriptions);
            var playback = board.PresentTransitionAsync(transition, afterAtEntry);
            Require(!playback.IsCompleted && board.IsPresenting, "Visibility mutation must occur during a real asynchronous turn announcement.");
            if (initiallyVisible)
                afterAtEntry.Clear();
            else
                afterAtEntry.Add(id);
            Require((await ObserveRecovery(board)).Count == (initiallyVisible ? 1 : 0) && await playback
                && board.RenderedUnitCount == (initiallyVisible ? 1 : 0),
                "Playback and final rendering must retain the entry visibility snapshot across awaits, not borrow a mutable caller set.");
        }

        var afterDeparture = EcmaJson.Parse(original.After.GetRawText())!.AsObject();
        var departed = afterDeparture["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == id);
        afterDeparture["units"]!.AsArray().Remove(departed);
        var missingActor = new BattlePresentationTransition(original.Revision, original.Before, original.Action, original.Events,
            EcmaJson.ToElement(afterDeparture), original.IsComputerAction, EventDescriptions: original.EventDescriptions);
        var missingPlayback = board.PresentTransitionAsync(missingActor);
        Require((await ObserveRecovery(board)).Count == 0 && await missingPlayback,
            "An isolated after-action departure fixture cannot leave a recovery ghost at the old position.");

        var completed = new List<int>();
        void Completed(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Completed;
        try
        {
            var cancelled = board.PresentTransitionAsync(original);
            for (var tick = 0; board.GetNodeOrNull("BattleEffects/Heal") is null && board.IsPresenting && tick < 200; tick++)
            {
                board.AdvancePresentation(0.05);
                await Task.Yield();
            }
            Require(board.GetNodeOrNull("BattleEffects/Heal") is not null && !completed.Contains(regenerationIndex),
                "Cancellation must interrupt a genuinely active recovery effect before its event completes.");
            board.CancelPresentation();
            Require(!await cancelled && !completed.Contains(regenerationIndex) && board.GetNodeOrNull("BattleEffects") is null,
                "Cancellation resolves playback and cannot announce an unfinished recovery event.");
            completed.Clear();
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(original);
            Require(reduced.IsCompleted && await reduced && completed.SequenceEqual(Enumerable.Range(0, original.Events.Count))
                && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced motion completes the same event sequence synchronously, with no residual recovery effect.");
            board.AnimationsEnabled = true;
        }
        finally
        {
            board.EventPresented -= Completed;
        }
        var fresh = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Require(fresh.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), 0).Accepted
            && fresh.Events.All(value => value["type"]!.GetValue<string>() != "regen"),
            "A real full-health opening refresh emits no recovery event.");
        var fullHealth = board.PresentTransitionAsync(fresh.GetSnapshot().Transition!);
        Require((await ObserveRecovery(board)).Count == 0 && await fullHealth,
            "Feedback must not infer healing from a turn start or a terrain rate when no actual recovery occurred.");
        Require(StateDigest.Hash(session.Snapshot()) == StateDigest.Hash(EcmaJson.Parse(original.After.GetRawText())!.AsObject()),
            "All projection boundary and lifetime probes preserve the committed battle.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
