using System.Collections.Frozen;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Presentation;
using Empire.Presentation.Tactical;

namespace Empire.Application;

public sealed partial class GrayFlagSession
{
    public ITacticalSelectionQueries GetTacticalQueries(long binding) => new SelectionQueries(this, binding);

    private sealed class SelectionQueries : ITacticalSelectionQueries
    {
        private readonly GrayFlagSession session;
        private readonly JsonObject state;
        private readonly TacticalStamp stamp;
        private readonly IReadOnlySet<int> visible;
        private readonly bool canUndo;
        private readonly FrozenSet<BoardCell> interactiveCells;

        public SelectionQueries(GrayFlagSession session, long binding)
        {
            this.session = session;
            stamp = new(binding, session.revision);
            state = session.battle.Snapshot();
            canUndo = session.CanUndo;
            visible = session.VisibleUnitIds(state, Viewer(state));
            var board = session.engine.Space.Board(state);
            interactiveCells = state["map"]!["tiles"]!.AsArray()
                .Select((tile, index) => (Terrain: EcmaJson.ReadString(tile), At: Cell(board.CoordOf(index))))
                .Where(cell =>
                {
                    var terrain = session.engine.Catalog.Definition("terrains", cell.Terrain)
                        ?? throw new DomainInvariantException($"Unknown terrain {cell.Terrain}");
                    return !terrain.GetProperty("tags").EnumerateArray().Any(tag => tag.ValueEquals("outside"));
                })
                .Select(cell => cell.At).ToFrozenSet();
            Check();
        }

        private static int Integer(JsonObject value, string key) => value[key]!.GetValue<int>();
        private static string Text(JsonObject value, string key) => EcmaJson.ReadString(value[key]);
        private static string Text(JsonNode? value, string _) => EcmaJson.ReadString(value);

        private void Check()
        {
            if (session.revision != stamp.Revision)
                throw new DomainInvariantException("Tactical queries belong to a stale session revision.");
        }

        private T Read<T>(Func<T> query)
        {
            Check();
            var result = query();
            Check();
            return result;
        }

        private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
        private static BoardCell Cell(Coord at) => new(at.X, at.Y);
        private static Coord Coord(BoardCell at) => new(at.X, at.Y);
        private IReadOnlyList<BoardCell> Cells(IEnumerable<Coord> values) => Freeze(values.Select(Cell));
        private IReadOnlyList<BoardCell> Indices(IEnumerable<int> values) => Cells(values.Select(session.engine.Space.Board(state).CoordOf));
        private JsonObject RequireVisible(int id) => visible.Contains(id)
            ? TacticalQueries.RequireUnit(state, id)
            : throw new DomainInvariantException("Unit is not visible in this tactical snapshot.");
        private TacticalUnit? Project(JsonObject? unit) => unit is null || !visible.Contains(Integer(unit, "id"))
            ? null : new(Integer(unit, "id"), Cell(TacticalQueries.Position(unit)), session.engine.Turns.MayAct(state, unit), true);
        private bool IsHuman => Text(TacticalQueries.Player(state, Integer(state, "currentPlayer")), "controller") == "human";
        public TacticalStamp Stamp => Read(() => stamp);
        public string Phase => Read(() => Text(state, "phase"));
        public bool HumanInput => Read(() => IsHuman && Text(state, "phase") != "over");
        public bool HumanTurn => Read(() => IsHuman && Text(state, "phase") == "playing");
        public bool CanUndo => Read(() => canUndo);
        public IReadOnlySet<BoardCell> InteractiveCells => Read(() => interactiveCells);
        public TacticalUnit? Unit(int id) => Read(() => Project(TacticalQueries.UnitById(state, id)));
        public TacticalUnit? UnitAt(BoardCell at) => Read(() => Project(TacticalQueries.UnitAt(state, Coord(at))));
        public bool AreEnemies(int firstUnit, int secondUnit) => Read(() => TacticalQueries.AreEnemies(state,
            Integer(RequireVisible(firstUnit), "owner"), Integer(RequireVisible(secondUnit), "owner")));
        public bool IsEnemyOfViewer(int unit) => Read(() => TacticalQueries.AreEnemies(state,
            Integer(RequireVisible(unit), "owner"), Viewer(state)));
        public bool IsSite(BoardCell at) => Read(() =>
        {
            var board = session.engine.Space.Board(state);
            return board.Contains(Coord(at)) && session.engine.Catalog.Definition("terrains",
                EcmaJson.ReadString(state["map"]!["tiles"]![board.IndexOf(Coord(at))]))!.Value.GetProperty("capturable").GetBoolean();
        });
        public bool CanOpenRecruitment(BoardCell at) => Read(() => HumanTurn &&
            TacticalQueries.UnitAt(state, Coord(at)) is null && session.engine.Recruitment.Cards(state, Coord(at)).Count > 0);
        public IReadOnlyList<int> Actors() => Read(() => Freeze((Phase == "playing" ? session.engine.Turns.Actors(state) : [])
            .Where(unit => visible.Contains(Integer(unit, "id"))).Select(unit => Integer(unit, "id"))));
        public IReadOnlyList<BoardCell> MoveStops(int unit) => Read(() => Indices(session.engine.Space.ComputeMoveField(state, RequireVisible(unit)).Stops));
        public IReadOnlyList<BoardCell>? PathTo(int unit, BoardCell at) => Read(() =>
        {
            var field = session.engine.Space.ComputeMoveField(state, RequireVisible(unit));
            return session.engine.Space.PathTo(field, state, Coord(at)) is { } path ? Cells(path) : null;
        });
        public IReadOnlyList<BoardCell> Threat(int unit) => Read(() =>
        {
            var actor = RequireVisible(unit);
            return Indices(session.engine.Space.ThreatTiles(state, actor, session.engine.Space.ComputeMoveField(state, actor)));
        });
        public IReadOnlyList<BoardCell> ControlZone(int unit) => Read(() => Indices(session.engine.Space.HostileControlZone(state, RequireVisible(unit))));
        public IReadOnlyList<TacticalCommand> CommandsAt(int unit, BoardCell at) => Read(() => Freeze(
            session.engine.CommandsAt(state, RequireVisible(unit), Coord(at)).Select(option =>
                new TacticalCommand(option.Key, option.Ability, option.Weapon, option.SelfTargeted, option.Engagement is not null, Freeze(option.Targets.Select(target => TargetSubject(option.TargetKind, target)))))));
        private TacticalTarget TargetSubject(AbilityTargetKind kind, Coord at)
        {
            if (kind is AbilityTargetKind.Self or AbilityTargetKind.Cell)
                return TacticalTarget.AtCell(Cell(at));
            var unit = TacticalQueries.UnitAt(state, at);
            if (unit is not null)
                return TacticalTarget.Unit(Cell(at), Integer(unit, "id"));
            if (kind == AbilityTargetKind.UnitOrStructure && TacticalQueries.StructureAt(state, at) is not null)
                return TacticalTarget.Object(Cell(at));
            throw new DomainInvariantException("Command target does not have its declared canonical subject.");
        }

        public TacticalAttack Attack(int unit, BoardCell target, BoardCell from, string weapon) => Read(() =>
        {
            var plan = session.engine.Combat.Forecast(state, RequireVisible(unit), Coord(target), Coord(from), weapon);
            var index = session.engine.Space.Board(state).IndexOf(Coord(from));
            var terrainId = Text(state["map"]!["tiles"]![index], "terrain");
            var definition = session.engine.Catalog.Definition("terrains", terrainId)
                ?? throw new DomainInvariantException($"Unknown terrain {terrainId}");
            // Decode through the canonical UTF-16 boundary, including unrelated authored strings.
            var terrain = EcmaJson.Parse(definition.GetRawText())!.AsObject();
            return new TacticalAttack(plan.PrimaryUnit?.Strike.Damage ?? 0,
                plan.UnitHits.Where(hit => !hit.Primary).Sum(hit => hit.Damage.Damage) +
                plan.StructureHits.Where(hit => !hit.Primary).Sum(hit => hit.Forecast.Damage),
                plan.PrimaryUnit?.Counter?.Damage ?? 0, terrain["defense"]!.GetValue<double>(),
                plan.PrimaryUnit is not null, Cells(plan.AffectedCells));
        });
        public IReadOnlyList<int> DeploymentUnits() => Read(() => Freeze(
            session.engine.Deployment.Roster(state)?.Units.Where(unit => visible.Contains(Integer(unit, "id")))
                .Select(unit => Integer(unit, "id")) ?? []));
        public IReadOnlyList<BoardCell> DeploymentZone() => Read(() => Cells(session.engine.Deployment.Roster(state)?.Zone ?? []));
        public IReadOnlyList<TacticalTarget> DeploymentTargets(int unit) => Read(() => Freeze(
            session.engine.Deployment.Spots(state, RequireVisible(unit)).Select(spot => spot.Swaps is { } swapped
                ? TacticalTarget.Unit(Cell(spot.At), Integer(swapped, "id")) : TacticalTarget.AtCell(Cell(spot.At)))));
        public IReadOnlyList<TacticalTactic> Tactics() => Read(() => Freeze(state["commanders"]!.AsArray().OfType<JsonObject>()
            .SelectMany(commander => session.engine.Support.Tactics(state, Text(commander, "id"))
                .Select(option => new TacticalTactic(Text(commander, "id"), option.Id, Cells(option.Targets))))));
        public IReadOnlyList<TacticalPassenger> Passengers(int carrier) => Read(() => Freeze(
            session.engine.Transport.PassengerOptions(state, RequireVisible(carrier)).Select(option =>
                new TacticalPassenger(Integer(option.Unit, "id"), Cells(option.Spots)))));
        public IReadOnlyList<BoardCell> DirectionSteps(BoardCell from) => Read(() =>
        {
            var grid = session.engine.Space.Board(state).Grid;
            var board = session.engine.Space.Board(state);
            return Cells(grid.Directions.Select(direction => grid.Step(Coord(from), direction.Id)).Where(board.Contains));
        });
    }
}
