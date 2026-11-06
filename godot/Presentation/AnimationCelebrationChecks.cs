#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private const int CelebrationTeam = 71;

    private async Task CheckCelebrationClips()
    {
        var shipped = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        foreach (var (type, key) in shipped.Units.Keys.Select(type => (type, (string?)null))
            .Concat(shipped.Actors.Select(pair => (pair.Value.ExpectedUnitType, (string?)pair.Key))))
        {
            var binding = key is null ? shipped.Units[type] : shipped.Actors[key].UnitArt;
            var clip = assets.ResolveUnitClip(type, "celebrate", ActorClipUnit.Milliseconds, key);
            var asset = assets.ResolveUnit(type, key);
            Require(binding.ActionClips["celebrate"] == "static.celebrate" && clip.Asset == asset
                && clip.Length == 220 && clip.FrameIndices.Count == 1 && clip.ReleaseMs is null
                && clip.FrameAt(0) == AssetCatalog.FrameIndex(asset, "idle") && clip.FrameAt(220) == clip.FrameAt(0),
                "Every shipped unit and named actor explicitly holds its existing idle picture for the 220 ms celebration beat.");
        }
        var (manifest, bindings) = CelebrationFixture();
        var catalog = new AssetCatalog(manifest, bindings);
        var state = CelebrationState();
        var board = NewActorClipBoard(catalog);
        try
        {
            await CheckCelebrationCohort(board, state);
            await CheckCelebrationBoundaries(board, state);
            await CheckCelebrationLifetime(board, catalog, state);
            ConfigureActorClipBoard(board, assets);
            var measuredTransition = Transition(initial, initial, IdleAction(),
                [CelebrationEvent(BattleConclusion.LastTeamRemaining(1))]);
            var setup = System.Diagnostics.Stopwatch.StartNew();
            var measured = board.PresentTransitionAsync(viewportFeedback, measuredTransition);
            setup.Stop();
            GD.Print($"Full C01 celebration receipt setup including rendering: {setup.Elapsed.TotalMilliseconds:F1} ms (synthetic event, no timing budget).");
            await Drain(board, measured);
            await CheckCelebrationEligibility(board, state);
        }
        finally { RemoveChild(board); board.Free(); }
        await CheckCelebrationLegalReceipt();
    }

    private static (AssetManifest Manifest, ArtBindings Bindings) CelebrationFixture()
    {
        var (manifest, bindings) = ReleaseFixture();
        // Existing trusted poses are timing fixtures, not newly accepted celebration artwork.
        manifest.ActorClips["fixture.celebrate-short"] = ReactionClip(128);
        manifest.ActorClips["fixture.celebrate-long"] = ReactionClip(384);
        bindings.Units["c01.swordsman"].ActionClips["celebrate"] = "fixture.celebrate-short";
        bindings.Actors["campaign-torren"].UnitArt.ActionClips["celebrate"] = "fixture.celebrate-long";
        return (manifest, bindings);
    }

    private JsonObject CelebrationState()
    {
        // Detached presentation fixtures, not a lawful campaign battle or fabricated combat receipt.
        var state = ReleaseState();
        state["units"] = new JsonArray(state["units"]!.AsArray().Take(8).Select(unit => unit!.DeepClone()).ToArray());
        foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>())
        {
            unit["type"] = "c01.swordsman";
            unit["owner"] = 1;
            unit.Remove("key");
        }
        state["units"]![1]!["key"] = "campaign-torren";
        state["units"]![1]!["owner"] = 3;
        state["players"]![0]!["team"] = CelebrationTeam;
        var ally = (JsonObject)state["players"]![0]!.DeepClone();
        ally["id"] = 3;
        ally["alive"] = false;
        state["players"]!.AsArray().Add(ally);
        return state;
    }

    private static JsonObject CelebrationEvent(BattleConclusion? conclusion = null) => new()
    {
        ["type"] = "gameOver",
        ["conclusion"] = (conclusion ?? BattleConclusion.ObjectiveCompleted(CelebrationTeam, 1, "fixture")).ToJson()
    };

    private async Task CheckCelebrationCohort(BoardView board, JsonObject state)
    {
        var before = (JsonObject)state.DeepClone();
        var after = (JsonObject)state.DeepClone();
        var ids = state["units"]!.AsArray().OfType<JsonObject>().Select(unit => Int(unit, "id")).ToArray();
        before["units"]![1]!["owner"] = 2;
        after["units"]![2]!["owner"] = 2;
        after["units"]![3]!["hp"] = 0;
        after["units"]!.AsArray().RemoveAt(4);
        before["units"]!.AsArray().RemoveAt(7);
        var visibleBefore = ids.Where(id => id != ids[5]).ToHashSet();
        var visibleAfter = ids.Where(id => id != ids[6]).ToHashSet();
        var transition = Transition(before, after, IdleAction(), [CelebrationEvent()], visibleBefore);
        var bytes = before.ToJsonString() + after.ToJsonString() + transition.Events[0].GetRawText();
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, transition, visibleAfter);
            var first = Actor(board, ids[0]);
            var second = Actor(board, ids[1]);
            var origins = new[] { first.Position, second.Position };
            var contacts = new[] { first.GetMeta("logical_contact").AsVector2(), second.GetMeta("logical_contact").AsVector2() };
            Require(Frame(first) == 1 && Frame(second) == 1 && published.Count == 0,
                "One terminal event starts all winners together using committed owners and team identity, including a defeated owner's surviving ally.");
            Require(new[] { 2, 3, 4, 6 }.All(index => Frame(Actor(board, ids[index])) == 0)
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{ids[5]}") is null
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{ids[7]}") is null,
                "Losing, dead, absent-after, hidden-before, hidden-after and undrawn new actors cannot join the winning cohort.");
            await Tick(board, .096);
            Require(Frame(first) == 3 && Frame(second) == 3 && !task.IsCompleted,
                "Winning actors sample their own authored clips concurrently rather than queueing by unit order.");
            Require(first.Position == origins[0] && second.Position == origins[1]
                && first.GetMeta("logical_contact").AsVector2() == contacts[0]
                && second.GetMeta("logical_contact").AsVector2() == contacts[1]
                && first.Scale == Vector2.One && second.Scale == Vector2.One
                && first.SelfModulate == Colors.White && second.SelfModulate == Colors.White,
                "Celebration never invents movement, contact, scale or opacity changes.");
            CheckClipShadow(first.GetNode<Sprite2D>("AuthoredSprite"));
            CheckClipShadow(second.GetNode<Sprite2D>("AuthoredSprite"));
            await Tick(board, .032);
            Require(Frame(first) == 0 && Frame(second) == 3 && !task.IsCompleted && published.Count == 0,
                "The 128 ms winner resumes its own idle while the 384 ms winner still holds the receipt gate.");
            await Tick(board, .256);
            Require(task.IsCompleted && await task && published.SequenceEqual(new[] { 0 })
                && !board.IsPresenting && Frame(Actor(board, ids[0])) == 0 && Frame(Actor(board, ids[1])) == 0,
                "The group completes once at its longest clip, without summing actor durations or appending another result delay.");
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{ids[4]}") is null
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{ids[6]}") is null
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null
                && before.ToJsonString() + after.ToJsonString() + transition.Events[0].GetRawText() == bytes,
                "Settlement removes ghosts and hidden actors, releases effects, and preserves every input snapshot and conclusion byte.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckCelebrationBoundaries(BoardView board, JsonObject state)
    {
        var pair = (JsonObject)state.DeepClone();
        pair["units"] = new JsonArray(state["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
        var ids = pair["units"]!.AsArray().OfType<JsonObject>().Select(unit => Int(unit, "id")).ToArray();
        foreach (var (ms, shortFrame, longFrame) in new (double, int, int)[]
        {
            (31.999, 1, 1), (32, 2, 2), (95.999, 2, 2), (96, 3, 3),
            (127.999, 3, 3), (128, 0, 3), (383.999, 0, 3), (384, 0, 0), (1000, 0, 0)
        })
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(pair, pair, IdleAction(), [CelebrationEvent(BattleConclusion.LastTeamRemaining(CelebrationTeam))]));
            await Tick(board, ms / 1000);
            Require(Frame(Actor(board, ids[0])) == shortFrame && Frame(Actor(board, ids[1])) == longFrame
                && task.IsCompleted == (ms >= 384),
                "Exact and coarse celebration samples preserve half-open frame intervals, individual endpoints and the longest-clip gate.");
            await Drain(board, task);
        }
        pair["units"]!.AsArray().RemoveAt(1);
        var shortOnly = board.PresentTransitionAsync(viewportFeedback, Transition(pair, pair, IdleAction(), [CelebrationEvent()]));
        await Tick(board, .128);
        Require(Frame(Actor(board, ids[0])) == 0 && !shortOnly.IsCompleted,
            "A short celebration returns idle without removing the original 220 ms result beat.");
        await Tick(board, .092);
        Require(shortOnly.IsCompleted && await shortOnly, "A short cohort ends at 220 ms, not 220 ms plus clip duration.");
    }

    private async Task CheckCelebrationLifetime(BoardView board, AssetCatalog catalog, JsonObject state)
    {
        var ids = state["units"]!.AsArray().Take(2).OfType<JsonObject>().Select(unit => Int(unit, "id")).ToArray();
        var transition = Transition(state, state, IdleAction(), [CelebrationEvent()]);
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var interruption in new[] { "cancel", "rebind", "tree-exit" })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .064);
                Require(ids.All(id => Frame(Actor(board, id)) == 2), "The interruption witness has concurrently active winning poses.");
                if (interruption == "cancel")
                    board.CancelPresentation();
                else if (interruption == "rebind")
                {
                    ConfigureActorClipBoard(board, catalog);
                    board.RenderState(state);
                }
                else
                    RemoveChild(board);
                Require(!await task && published.Count == 0 && !board.IsPresenting,
                    "Cancel, rebind and tree exit retire the entire celebration without publishing its unfinished event.");
                if (interruption == "tree-exit")
                {
                    AddChild(board);
                    ConfigureActorClipBoard(board, catalog);
                    board.RenderState(state);
                }
                await Tick(board, 1);
                Require(ids.All(id => Frame(Actor(board, id)) == 0 && Actor(board, id).Scale == Vector2.One
                    && Actor(board, id).SelfModulate == Colors.White) && board.GetNodeOrNull("BattleEffects") is null
                    && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                    "Later ticks cannot resurrect a cancelled cohort or leave stale poses, effects or viewport ownership.");
            }
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(reduced.IsCompleted && await reduced && published.SequenceEqual(new[] { 0 })
                && ids.All(id => Frame(Actor(board, id)) == 0) && !board.IsPresenting
                && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                "Reduced motion settles every winner and the receipt synchronously with no residual ownership.");
            board.AnimationsEnabled = true;
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckCelebrationEligibility(BoardView board, JsonObject state)
    {
        var (manifest, bindings) = CelebrationFixture();
        bindings.Units["c01.swordsman"].ActionClips.Remove("celebrate");
        ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
        var pair = (JsonObject)state.DeepClone();
        // Named winner is prepared first, exposing partial-cohort cleanup if the next eligible binding fails.
        pair["units"] = new JsonArray(state["units"]![1]!.DeepClone(), state["units"]![0]!.DeepClone());
        var named = Int(pair["units"]![0]!.AsObject(), "id");
        var missing = Int(pair["units"]![1]!.AsObject(), "id");
        foreach (var exclusion in new[] { "loser", "dead", "absent", "before-hidden", "after-hidden", "undrawn" })
        {
            var before = (JsonObject)pair.DeepClone();
            var after = (JsonObject)pair.DeepClone();
            var beforeVisible = new HashSet<int> { named, missing };
            var afterVisible = new HashSet<int> { named, missing };
            switch (exclusion)
            {
                case "loser":
                    after["units"]![1]!["owner"] = 2;
                    break;
                case "dead":
                    after["units"]![1]!["hp"] = 0;
                    break;
                case "absent":
                    after["units"]!.AsArray().RemoveAt(1);
                    break;
                case "before-hidden":
                    beforeVisible.Remove(missing);
                    break;
                case "after-hidden":
                    afterVisible.Remove(missing);
                    break;
                case "undrawn":
                    before["units"]!.AsArray().RemoveAt(1);
                    break;
            }
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, after, IdleAction(), [CelebrationEvent()], beforeVisible), afterVisible);
            Require(!task.IsFaulted && Frame(Actor(board, named)) == 1,
                "Eligibility and both visibility gates precede clip lookup; excluded missing art cannot fault or disclose a celebration.");
            await Drain(board, task);
        }
        var failed = board.PresentTransitionAsync(viewportFeedback, Transition(pair, pair, IdleAction(), [CelebrationEvent()]));
        var rejected = false;
        try
        {
            await failed;
        }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && !board.IsPresenting && Frame(Actor(board, named)) == 0 && Frame(Actor(board, missing)) == 0
            && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
            "An eligible missing celebration rejects explicitly, cleans already prepared winners, and never falls back to idle art.");
        await Tick(board, 1);
        Require(Frame(Actor(board, named)) == 0, "Failed group preparation cannot resume after settlement.");

        foreach (var conclusion in new[] { BattleConclusion.AllDefeated(), BattleConclusion.TurnLimitReached(), BattleConclusion.NoActors() })
        {
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(pair, pair, IdleAction(), [CelebrationEvent(conclusion)]));
            await Tick(board, .219999);
            Require(!task.IsCompleted && Frame(Actor(board, named)) == 0 && Frame(Actor(board, missing)) == 0,
                "Every null-winner conclusion skips actor art and preserves only the original result beat.");
            await Tick(board, .000001);
            Require(task.IsCompleted && await task, "A null-winner result completes exactly at 220 ms.");
        }
        pair["phase"] = "over";
        pair["conclusion"] = BattleConclusion.LastTeamRemaining(CelebrationTeam).ToJson();
        var noEvent = board.PresentTransitionAsync(viewportFeedback, Transition(pair, pair, IdleAction(), Array.Empty<JsonObject>()));
        Require(noEvent.IsCompleted && await noEvent && Frame(Actor(board, named)) == 0 && Frame(Actor(board, missing)) == 0,
            "A terminal snapshot without a gameOver event cannot infer or replay celebration, even with a missing winner binding.");
    }

    private async Task CheckCelebrationLegalReceipt()
    {
        // Synthetic lawful common-content rout, not a formal campaign witness. Commands own every HP/outcome change.
        JsonObject Player(int id) => new()
        {
            ["id"] = id,
            ["team"] = id == 1 ? CelebrationTeam : 2,
            ["color"] = id == 1 ? "#336699" : "#993333",
            ["controller"] = "human",
            ["resources"] = new JsonObject(),
            ["objectives"] = new JsonArray(new JsonObject { ["id"] = "rout", ["type"] = "routEnemies" })
        };
        var fixtureLevel = new JsonObject
        {
            ["schema"] = 3,
            ["id"] = "fixture.celebration",
            ["width"] = 6,
            ["height"] = 4,
            ["terrain"] = new JsonArray("......", "......", "......", "......"),
            ["units"] = new JsonArray(
                new JsonObject { ["unit"] = "soldier", ["owner"] = 1, ["x"] = 1, ["y"] = 1, ["facing"] = "east" },
                new JsonObject { ["unit"] = "archer", ["owner"] = 1, ["x"] = 1, ["y"] = 2, ["facing"] = "east" },
                new JsonObject { ["unit"] = "soldier", ["owner"] = 2, ["x"] = 2, ["y"] = 1, ["facing"] = "west" }),
            ["players"] = new JsonArray(Player(1), Player(2)),
            ["rules"] = new JsonObject
            {
                ["fog"] = false,
                ["zoneOfControl"] = false,
                ["moraleEnabled"] = false,
                ["counterAttack"] = false,
                ["turnLimit"] = null
            }
        };
        var engine = new BattleEngine(packs);
        var battle = engine.CreateBattle(fixtureLevel);
        var ids = battle.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Select(unit => Int(unit, "id")).ToArray();
        var order = new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = ids[0],
            ["path"] = new JsonArray(At(1, 1)),
            ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = "soldier_sword", ["target"] = At(2, 1) }
        };
        JsonObject? terminalBefore = null;
        IReadOnlyList<JsonObject>? terminalEvents = null;
        for (var attack = 0; attack < 12; attack++)
        {
            var receipt = battle.Dispatch(order);
            if (receipt.Events.Any(item => item["type"]!.GetValue<string>() == "gameOver"))
            {
                terminalBefore = receipt.Before;
                terminalEvents = receipt.Events;
                break;
            }
            battle.Dispatch(IdleAction());
            battle.Dispatch(IdleAction());
        }
        Require(terminalBefore is not null && terminalEvents is not null
            && BattleConclusion.FromState(battle.Snapshot())?.Team == CelebrationTeam,
            "Unmodified common rules reach a genuine winning receipt through legal attacks and turn changes.");
        var expectedHash = battle.StateHash;
        var events = terminalEvents!;
        var expectedEvents = new JsonArray(events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString();
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = SessionPresentationText.Compose(new(engine, text, new Dictionary<string, string>()),
            new JsonArray(fixtureLevel.DeepClone()), """
            {"schema":3,"levels":{"fixture.celebration":{
              "title":{"literal":"Celebration timing fixture"},"description":{"literal":"Synthetic lawful rout"},
              "signals":{},"actors":{},"players":{
                "1":{"name":{"literal":"Allies"},"objectives":{"rout":{"generated":true}}},
                "2":{"name":{"literal":"Opponents"},"objectives":{"rout":{"generated":true}}}
              }}}}
            """, Godot.FileAccess.GetFileAsString("res://data/grayflag-content-text.json"));
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(terminalBefore!));
        var (manifest, bindings) = CelebrationFixture();
        manifest.ActorClips["fixture.celebrate-soldier"] = new()
        {
            Unit = "milliseconds",
            Frames = ReactionClip(128).Frames
        };
        bindings.Units["soldier"].ActionClips["celebrate"] = "fixture.celebrate-soldier";
        manifest.ActorClips["fixture.celebrate-archer"] = new()
        {
            Unit = "milliseconds",
            Frames = ReactionClip(384).Frames
        };
        bindings.Units["archer"].ActionClips["celebrate"] = "fixture.celebrate-archer";
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            var board = screen.Board;
            board.AdvanceAnimationsAutomatically = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(new AssetCatalog(manifest, bindings), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                fixtureLevel, new AuthoredScene { LevelId = "fixture.celebration", MapSize = new[] { 6, 4 }, RuntimeReady = true, FramePlacements = new() });
            var published = new List<int>();
            board.EventPresented += (_, index) => published.Add(index);
            Require(session.Dispatch(EcmaJson.ToElement(order), session.Revision).Accepted,
                "BattleScreen receives the genuine terminal attack from the restored lawful pre-commit state.");
            var resultIndex = events.ToList().FindIndex(item => item["type"]!.GetValue<string>() == "gameOver");
            screen.Refresh();
            screen.Refresh();
            for (var step = 0; step < 1000 && published.Count < resultIndex; step++)
                await Tick(board, .010);
            Require(published.Count == resultIndex && Frame(Actor(board, ids[0])) == 1 && Frame(Actor(board, ids[1])) == 1
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{ids[2]}") is null && screen.IsInputBlocked,
                "The real terminal event starts both surviving winners after the losing actor's actual death and keeps native input gated.");
            await Tick(board, .221);
            screen.Refresh();
            Require(screen.IsPresenting && screen.IsInputBlocked && Frame(Actor(board, ids[0])) == 0
                && Frame(Actor(board, ids[1])) == 3 && published.Count == resultIndex,
                "Repeated native refresh cannot bypass the longer winner clip after the original result beat expires.");
            await Tick(board, .170);
            Require(!screen.IsPresenting && session.IsConcluded && !session.CanUndo
                && published.SequenceEqual(Enumerable.Range(0, events.Count)),
                "The native terminal receipt completes once in domain order while the concluded battle remains non-reversible.");
            var finishedRevision = session.Revision;
            Require(!session.Dispatch(EcmaJson.ToElement(IdleAction()), finishedRevision).Accepted && session.Revision == finishedRevision,
                "Finishing presentation does not reopen orders or create another revision in the concluded battle.");
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            await Tick(board, 1);
            Require(!screen.IsPresenting && published.Count == events.Count && screen.RecentMessages.SequenceEqual(messages)
                && session.ActionLog.Count == 1 && StateDigest.Hash(session.Snapshot()) == expectedHash
                && new JsonArray(session.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()).ToJsonString() == expectedEvents,
                "Settled screen refreshes cannot replay celebrations, duplicate logs or change the genuine terminal state and event bytes.");
        }
        finally { RemoveChild(screen); screen.Free(); }
    }
}
