#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

internal static class BattleScreenMovementQueryChecks
{
    public static void Check()
    {
        RecordingSpace? recording = null;
        var engine = BattleEngineFactory.Create(new ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")),
            [new EnginePlugin("test.native-movement-queries", 1, context =>
            {
                recording = new(new(context.Require(BattleCapabilities.Content), context.Require(BattleCapabilities.Grids)));
                context.Replace<ITacticalSpace>(BattleCapabilities.Space, recording);
            }, overrides: [BattleCapabilities.Space], requiresCapabilities: [BattleCapabilities.Content, BattleCapabilities.Grids])]);
        var space = recording ?? throw new InvalidOperationException("The recording spatial plugin was not installed.");
        Require(ReferenceEquals(engine.Space, space), "The session must use the composed spatial replacement.");
        var rules = new BattleSessionRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()), new JsonArray());
        CheckCampaignResults(rules, space);
        CheckCacheIsolation(rules, space);
        CheckInvisibleUnits(rules, space);
        CheckRevisionLifetime(rules, space);
        CheckReadPostcondition(rules, space);
        CheckMutableExtension(rules, space);
        Godot.GD.Print("MOVEMENT_QUERY_CONTRACTS passed: 16 shipped levels / 32 actors; lazy reuse, field identity, adapter/session/reload isolation, visibility, stale revisions, read postcondition, mutable extensions and readonly projections.");
    }

    private static void CheckCampaignResults(BattleSessionRules rules, RecordingSpace space)
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().ToArray();
        Require(levels.Length == 16, "Movement-query parity must cover all sixteen shipped levels.");
        foreach (var level in levels)
        {
            var session = new GrayFlagSession(rules, level);
            var before = EcmaJson.Stringify(session.Snapshot());
            var calls = space.Computed.Count;
            var queries = session.GetTacticalQueries(7);
            var actors = queries.Actors().Take(2).ToArray();
            Require(actors.Length == 2 && space.Computed.Count == calls,
                $"{level["id"]}: creating and inspecting an adapter must not eagerly compute movement.");
            foreach (var id in actors)
            {
                // The oracle uses the composed policy on a separate snapshot, not a second pathfinding implementation.
                var state = session.Snapshot();
                var actor = TacticalQueries.RequireUnit(state, id);
                var field = rules.Engine.Space.ComputeMoveField(state, actor);
                var board = rules.Engine.Space.Board(state);
                var stops = field.Stops.Select(index => Cell(board.CoordOf(index))).ToArray();
                var threat = rules.Engine.Space.ThreatTiles(state, actor, field).Select(index => Cell(board.CoordOf(index))).ToArray();
                Require(stops.Length > 0, "A shipped actor must have an origin stop.");
                var destinations = new[] { stops[0], stops[^1], new BoardCell(-1, -1) };
                var paths = destinations.Select(at => rules.Engine.Space.PathTo(field, state, Coord(at))?.Select(Cell).ToArray()).ToArray();
                calls = space.Computed.Count;
                var pathCalls = space.Paths.Count;
                var threatCalls = space.Threats.Count;
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    Require(queries.MoveStops(id).SequenceEqual(stops), $"{level["id"]}/{id}: cached stops changed their order or contents.");
                    Require(queries.Threat(id).SequenceEqual(threat), $"{level["id"]}/{id}: cached threat changed its order or contents.");
                    for (var index = 0; index < destinations.Length; index++)
                        Require(SamePath(queries.PathTo(id, destinations[index]), paths[index]),
                            $"{level["id"]}/{id}: cached path differs from the composed spatial policy.");
                }
                Require(space.Computed.Count == calls + 1, $"{level["id"]}/{id}: repeated movement queries must compute exactly one field.");
                var cached = space.Computed[^1];
                Require(cached.Unit == id && !ReferenceEquals(cached.Field, field)
                    && !ReferenceEquals(cached.State, state)
                    && space.Paths.Count == pathCalls + 2 * destinations.Length && space.Threats.Count == threatCalls + 2
                    && space.Paths.Skip(pathCalls).All(value => ReferenceEquals(value, cached.Field))
                    && space.Threats.Skip(threatCalls).All(value => ReferenceEquals(value, cached.Field)),
                    "Path and threat policies must receive the same adapter-owned field, computed on its own snapshot.");
            }
            Require(session.Revision == 0 && !session.CanUndo && session.ActionLog.Count == 0 && session.Events.Count == 0
                && EcmaJson.Stringify(session.Snapshot()) == before,
                "Movement queries must preserve state, revision, ordered events, action history and undo availability.");
        }
    }

    private static void CheckCacheIsolation(BattleSessionRules rules, RecordingSpace space)
    {
        var session = new GrayFlagSession(rules, BattleTextChecks.OpeningLevel());
        var queries = session.GetTacticalQueries(9);
        var sameBinding = session.GetTacticalQueries(9);
        var otherBinding = session.GetTacticalQueries(10);
        var otherSession = new GrayFlagSession(rules, BattleTextChecks.OpeningLevel()).GetTacticalQueries(9);
        var reloaded = GrayFlagSession.FromSave(rules, session.Save()).GetTacticalQueries(9);
        var actors = queries.Actors().Take(2).ToArray();
        var calls = space.Computed.Count;
        var origin = queries.Unit(actors[0])!.At;
        Require(queries.PathTo(actors[0], origin) is { Count: 1 }, "A path-first query must retain the canonical origin path.");
        queries.Threat(actors[0]);
        queries.MoveStops(actors[0]);
        Require(space.Computed.Count == calls + 1, "Path-first lookup must populate the shared field cache.");
        var first = space.Computed[^1].Field;
        queries.Threat(actors[1]);
        queries.MoveStops(actors[1]);
        queries.PathTo(actors[1], queries.Unit(actors[1])!.At);
        Require(space.Computed.Count == calls + 2 && !ReferenceEquals(first, space.Computed[^1].Field),
            "Threat-first lookup must reuse its own field, isolated from another unit in the same adapter.");
        var fields = new List<MoveField> { first, space.Computed[^1].Field };
        foreach (var adapter in new[] { sameBinding, otherBinding, otherSession, reloaded })
        {
            calls = space.Computed.Count;
            adapter.MoveStops(actors[0]);
            adapter.PathTo(actors[0], origin);
            adapter.Threat(actors[0]);
            Require(space.Computed.Count == calls + 1 && fields.All(field => !ReferenceEquals(field, space.Computed[^1].Field)),
                "Different adapters, bindings, sessions and reloaded saves must never share mutable movement fields.");
            fields.Add(space.Computed[^1].Field);
        }
        calls = space.Computed.Count;
        queries.MoveStops(actors[0]);
        Require(space.Computed.Count == calls, "Queries on another adapter must not evict this adapter's field.");
    }

    private static void CheckInvisibleUnits(BattleSessionRules rules, RecordingSpace space)
    {
        var session = new GrayFlagSession(rules, BattleTextChecks.OpeningLevel());
        var hidden = session.GetTacticalQueries(0).Actors()[0];
        space.HiddenUnit = hidden;
        try
        {
            var queries = session.GetTacticalQueries(0);
            Require(queries.Unit(hidden) is null && TacticalQueries.UnitById(session.Snapshot(), hidden) is not null,
                "The visibility replacement must conceal a real shipped unit.");
            var visible = queries.Actors()[0];
            queries.MoveStops(visible);
            Require(TacticalQueries.UnitById(space.Computed[^1].State, hidden) is not null,
                "Visibility must filter query access, not remove concealed units from the spatial policy's battlefield snapshot.");
            var calls = space.Computed.Count;
            var pathCalls = space.Paths.Count;
            var threatCalls = space.Threats.Count;
            foreach (var id in new[] { hidden, int.MaxValue })
                foreach (var read in MovementReads(queries, id, new(0, 0)))
                    ExpectInvariant(read, "not visible");
            Require(space.Computed.Count == calls && space.Paths.Count == pathCalls && space.Threats.Count == threatCalls,
                "Hidden and unknown units must fail before invoking any movement policy.");
        }
        finally { space.HiddenUnit = null; }
    }

    private static void CheckRevisionLifetime(BattleSessionRules rules, RecordingSpace space)
    {
        var session = new GrayFlagSession(rules, BattleTextChecks.OpeningLevel());
        var original = EcmaJson.Stringify(session.Snapshot());
        var old = session.GetTacticalQueries(0);
        var id = old.Actors()[0];
        var at = old.Unit(id)!.At;
        old.MoveStops(id);
        var first = space.Computed[^1].Field;
        Wait(session, id, at);
        AssertStale(old, id, at, space);
        var current = session.GetTacticalQueries(0);
        var calls = space.Computed.Count;
        current.MoveStops(id);
        current.PathTo(id, at);
        current.Threat(id);
        var second = space.Computed[^1].Field;
        Require(space.Computed.Count == calls + 1 && !ReferenceEquals(first, second),
            "The adapter after dispatch must compute a new field, including for an acted visible unit.");
        Require(session.Undo(session.Revision).Accepted, "The real wait command must be undoable.");
        AssertStale(old, id, at, space);
        AssertStale(current, id, at, space);
        calls = space.Computed.Count;
        var restored = session.GetTacticalQueries(0);
        restored.Threat(id);
        restored.MoveStops(id);
        restored.PathTo(id, at);
        Require(space.Computed.Count == calls + 1 && !ReferenceEquals(space.Computed[^1].Field, first)
            && !ReferenceEquals(space.Computed[^1].Field, second) && session.Revision == 2
            && session.Events.Count == 0 && session.ActionLog.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == original,
            "Undo must restore battle facts without reviving fields from either stale revision.");
    }

    private static void CheckReadPostcondition(BattleSessionRules rules, RecordingSpace space)
    {
        var session = new GrayFlagSession(rules, BattleTextChecks.OpeningLevel());
        var queries = session.GetTacticalQueries(0);
        var id = queries.Actors()[0];
        var at = queries.Unit(id)!.At;
        queries.MoveStops(id);
        space.AfterPath = () => Wait(session, id, at);
        try
        {
            ExpectInvariant(() => queries.PathTo(id, at), "stale");
            Require(session.Revision == 1, "The post-read guard must be tested by a successful command inside the policy callback.");
            AssertStale(queries, id, at, space);
        }
        finally { space.AfterPath = null; }
    }

    private static void CheckMutableExtension(BattleSessionRules rules, RecordingSpace space)
    {
        var session = new GrayFlagSession(rules, BattleTextChecks.OpeningLevel());
        var before = EcmaJson.Stringify(session.Snapshot());
        var queries = session.GetTacticalQueries(0);
        var id = queries.Actors()[0];
        var origin = queries.Unit(id)!.At;
        var state = session.Snapshot();
        var board = space.Board(state);
        var destination = queries.InteractiveCells.First(at => at != origin);
        var index = board.IndexOf(Coord(destination));
        space.ConfigureField = (_, field) =>
        {
            field.Tiles = new();
            field.Stops = new() { index };
        };
        space.PathOverride = new() { Coord(origin), Coord(destination) };
        space.ThreatOverride = new() { index };
        try
        {
            var calls = space.Computed.Count;
            var stops = queries.MoveStops(id);
            var path = queries.PathTo(id, destination)!;
            var threat = queries.Threat(id);
            var field = space.Computed[^1].Field;
            Require(stops.SequenceEqual(new[] { destination }) && path.SequenceEqual(new[] { origin, destination })
                && threat.SequenceEqual(new[] { destination }) && field.Tiles.Count == 0
                && ReferenceEquals(space.Paths[^1], field) && ReferenceEquals(space.Threats[^1], field),
                "Custom Stops, PathTo and ThreatTiles must remain authoritative even with no traceable tiles.");
            foreach (var output in new[] { stops, path, threat })
                AssertReadOnly(output);
            field.Unit = -1;
            field.Origin = Coord(destination);
            field.Stops = new() { board.IndexOf(Coord(origin)) };
            space.PathOverride.Clear();
            space.PathOverride.Add(Coord(destination));
            space.ThreatOverride.Clear();
            Require(stops.SequenceEqual(new[] { destination }) && path.SequenceEqual(new[] { origin, destination })
                && threat.SequenceEqual(new[] { destination }), "Published query lists must be detached from extension-owned mutable results.");
            Require(queries.MoveStops(id).SequenceEqual(new[] { origin })
                && queries.PathTo(id, destination)!.SequenceEqual(new[] { destination }) && queries.Threat(id).Count == 0
                && space.Computed.Count == calls + 1 && ReferenceEquals(space.Paths[^1], field) && ReferenceEquals(space.Threats[^1], field),
                "Cache the mutable policy field by requested unit, not copied fields or previously projected lists.");
            Require(session.Revision == 0 && session.Events.Count == 0 && session.ActionLog.Count == 0
                && EcmaJson.Stringify(session.Snapshot()) == before, "Extension-owned field mutation must not mutate the battle.");
        }
        finally
        {
            space.ConfigureField = null;
            space.PathOverride = null;
            space.ThreatOverride = null;
        }
    }

    private static void Wait(GrayFlagSession session, int id, BoardCell at) => Require(session.Dispatch(EcmaJson.ToElement(new JsonObject
    {
        ["kind"] = "command",
        ["unit"] = id,
        ["path"] = TacticalQueries.PathJson([Coord(at)]),
        ["command"] = new JsonObject { ["ability"] = "wait" }
    }), session.Revision).Accepted, "The shipped opening must accept an explicit wait command.");

    private static IEnumerable<Action> MovementReads(ITacticalSelectionQueries queries, int id, BoardCell at) =>
        new Action[] { () => queries.MoveStops(id), () => queries.PathTo(id, at), () => queries.Threat(id) };

    private static void AssertStale(ITacticalSelectionQueries queries, int id, BoardCell at, RecordingSpace space)
    {
        var calls = (space.Computed.Count, space.Paths.Count, space.Threats.Count);
        foreach (var read in MovementReads(queries, id, at))
            ExpectInvariant(read, "stale");
        Require(calls == (space.Computed.Count, space.Paths.Count, space.Threats.Count),
            "A hot stale adapter must reject reads before returning cached data or invoking spatial policy.");
    }

    private static void ExpectInvariant(Action read, string reason)
    {
        try
        {
            read();
        }
        catch (DomainInvariantException error) when (error.Message.Contains(reason, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException($"Movement query must reject {reason} input with a domain invariant error.");
    }

    private static void AssertReadOnly(IReadOnlyList<BoardCell> output)
    {
        if (output is not IList<BoardCell> list)
            return;
        RejectWrite(() => list[0] = new(-1, -1));
        RejectWrite(() => list.Add(new(-1, -1)));
    }

    private static void RejectWrite(Action write)
    {
        try
        {
            write();
        }
        catch (NotSupportedException) { return; }
        throw new InvalidOperationException("Movement query output allowed caller mutation.");
    }

    private static bool SamePath(IReadOnlyList<BoardCell>? actual, IReadOnlyList<BoardCell>? expected) =>
        actual is null ? expected is null : expected is not null && actual.SequenceEqual(expected);
    private static BoardCell Cell(Coord at) => new(at.X, at.Y);
    private static Coord Coord(BoardCell at) => new(at.X, at.Y);
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingSpace(TacticalSpaceRules rules) : DefaultTacticalSpace(rules)
    {
        public List<(int Unit, JsonObject State, MoveField Field)> Computed { get; } = new();
        public List<MoveField> Paths { get; } = new();
        public List<MoveField?> Threats { get; } = new();
        public int? HiddenUnit
        {
            get; set;
        }
        public Action<JsonObject, MoveField>? ConfigureField
        {
            get; set;
        }
        public List<Coord>? PathOverride
        {
            get; set;
        }
        public HashSet<int>? ThreatOverride
        {
            get; set;
        }
        public Action? AfterPath
        {
            get; set;
        }

        public override MoveField ComputeMoveField(JsonObject state, JsonObject unit)
        {
            var field = base.ComputeMoveField(state, unit);
            ConfigureField?.Invoke(state, field);
            Computed.Add((unit["id"]!.GetValue<int>(), state, field));
            return field;
        }

        public override IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord destination)
        {
            Paths.Add(field);
            var result = PathOverride ?? base.PathTo(field, state, destination);
            var after = AfterPath;
            AfterPath = null;
            after?.Invoke();
            return result;
        }

        public override IReadOnlySet<int> ThreatTiles(JsonObject state, JsonObject unit, MoveField? field = null)
        {
            Threats.Add(field);
            return ThreatOverride ?? base.ThreatTiles(state, unit, field);
        }

        public override bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null) =>
            unit["id"]!.GetValue<int>() != HiddenUnit && base.IsUnitVisible(state, viewer, unit, seen);
    }
}
