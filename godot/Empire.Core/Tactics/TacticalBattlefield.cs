using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed class TacticalBattlefield
{
    internal StateContent Content { get; }
    public JsonObject State { get; }
    public JsonObject Map { get; }
    private readonly GridExtent extent;

    internal TacticalBattlefield(JsonObject state, StateContent content)
    {
        State = state;
        Content = content;
        Map = StateJson.Object(state["map"], "map");
        extent = new(StateJson.Integer(Map, "width"), StateJson.Integer(Map, "height"));
    }

    public bool Contains(Coord at) => GridStorage.InBounds(extent, at.X, at.Y);
    public int IndexOf(Coord at) => GridStorage.Index(extent, at.X, at.Y);

    public TacticalCell Cell(Coord at)
    {
        if (!Contains(at)) throw new DomainInvariantException($"battlefield cell out of bounds: {at.X},{at.Y}");
        return new TacticalCell(this, at);
    }

    public bool IsCliff(Coord from, Coord to)
    {
        var key = GridStorage.EdgeKey(from, to);
        return StateJson.Objects(Map["cliffs"], "cliffs").Any(edge =>
            GridStorage.EdgeKey(TacticalQueries.Position(StateJson.Object(edge["from"], "cliff from")),
                TacticalQueries.Position(StateJson.Object(edge["to"], "cliff to"))) == key);
    }

    public IReadOnlyList<JsonObject> OverlaysAt(Coord at)
    {
        var scenario = StateJson.Object(State["scenario"], "scenario");
        return StateJson.Objects(scenario["overlays"], "overlays")
            .Where(overlay => StateJson.Objects(overlay["cells"], "overlay cells")
                .Any(cell => TacticalQueries.Position(cell) == at)).ToArray();
    }

    public JsonObject DirectionalCoverAt(Coord at)
    {
        var cover = StateJson.Objects(Map["directionalCover"], "directionalCover")
            .FirstOrDefault(entry => TacticalQueries.Position(StateJson.Object(entry["at"], "cover at")) == at);
        return cover is null ? new JsonObject() : StateJson.Object(cover["sides"], "cover sides");
    }

    public double? TraversalCost(Coord from, Coord to, string movementClass)
    {
        var profile = Content.Read("movementProfiles", movementClass);
        var destination = Cell(to);
        var cost = destination.MovementCost(movementClass);
        if (cost is null || destination.BlocksMovement) return null;
        if (IsCliff(from, to) && !(profile.TryGetProperty("ignoresCliffs", out var ignoresCliffs) && StateJson.IsTrue(ignoresCliffs))) return null;
        var delta = destination.Elevation - Cell(from).Elevation;
        if (profile.TryGetProperty("maxClimb", out var maxClimb) && maxClimb.ValueKind != JsonValueKind.Null && delta > StateJson.Number(maxClimb, "maxClimb")) return null;
        if (profile.TryGetProperty("maxDrop", out var maxDrop) && maxDrop.ValueKind != JsonValueKind.Null && -delta > StateJson.Number(maxDrop, "maxDrop")) return null;
        return cost.Value + Math.Max(0, delta) * StateJson.Number(profile, "uphillCostPerLevel", 0);
    }
}

public sealed class TacticalCell
{
    private readonly TacticalBattlefield battlefield;
    public Coord At { get; }
    public int Index { get; }

    internal TacticalCell(TacticalBattlefield battlefield, Coord at)
    {
        this.battlefield = battlefield;
        At = at;
        Index = battlefield.IndexOf(at);
    }

    public JsonElement Terrain => battlefield.Content.Read("terrains",
        StateJson.Text(StateJson.Array(battlefield.Map["tiles"], "tiles")[Index], "terrain id"));
    public double Elevation => StateJson.Number(StateJson.Array(battlefield.Map["elevation"], "elevation")[Index], "elevation");
    public JsonObject? Structure => TacticalQueries.StructureAt(battlefield.State, At);
    public JsonObject? Occupant => TacticalQueries.UnitAt(battlefield.State, At);
    public IReadOnlyList<JsonObject> OverlayStates => battlefield.OverlaysAt(At);
    public IReadOnlyList<JsonElement> Overlays => OverlayStates
        .Select(overlay => battlefield.Content.Read("terrainOverlays", StateJson.Text(overlay, "type"))).ToArray();

    public double? MovementCost(string movementClass)
    {
        var costs = Terrain.GetProperty("cost");
        if (!Empire.Core.Determinism.EcmaJson.TryGetProperty(costs, movementClass, out var cost) || cost.ValueKind == JsonValueKind.Null) return null;
        var profile = battlefield.Content.Read("movementProfiles", movementClass);
        var tags = profile.GetProperty("tags").EnumerateArray()
            .Select(tag => StateJson.Text(tag)).ToHashSet(StringComparer.Ordinal);
        var delta = 0.0;
        foreach (var overlay in Overlays)
        {
            foreach (var entry in overlay.GetProperty("blockedMovementClasses").EnumerateArray())
            {
                var blocked = StateJson.Text(entry);
                if (blocked == movementClass || tags.Contains(blocked)) return null;
            }
            delta += StateJson.Number(overlay, "movementCostDelta", 0);
        }
        return Math.Max(1, StateJson.Number(cost, "terrain cost") + delta);
    }

    private JsonElement? StructureDefinition => Structure is { } structure
        ? battlefield.Content.Read("structures", StateJson.Text(structure, "type")) : null;
    private double OverlaySum(string field) => Overlays.Sum(overlay => StateJson.Number(overlay, field, 0));

    public double ObstructionTop => Elevation + Math.Max(StateJson.Number(Terrain, "obstructionHeight", 0),
        StructureDefinition is { } definition ? StateJson.Number(definition, "obstructionHeight", 0) : 0);
    public bool BlocksMovement => StructureDefinition is { } definition && StateJson.IsTrue(definition.GetProperty("blocksMovement"));
    public bool BlocksVision => StateJson.IsTrue(Terrain.GetProperty("opaque")) ||
        (StructureDefinition is { } definition && StateJson.IsTrue(definition.GetProperty("blocksVision")));
    public double Defense => Math.Max(0, StateJson.Number(Terrain, "defense", 0) + OverlayDefense);
    public double Vision => StateJson.Number(Terrain, "vision", 0) + OverlayVision;
    public double Heal => Math.Max(0, StateJson.Number(Terrain, "heal", 0) + OverlayHeal);
    public double OverlayDefense => OverlaySum("defenseDelta");
    public double OverlayVision => OverlaySum("visionDelta");
    public double OverlayHeal => OverlaySum("healDelta");
    public bool Admits(string movementClass) => !BlocksMovement && MovementCost(movementClass) is not null;
    public bool CanReceive(string movementClass) => Occupant is null && Admits(movementClass);

    public string Cover
    {
        get
        {
            var terrain = StateJson.Text(Terrain, "cover");
            var structure = StructureDefinition is { } definition ? StateJson.Text(definition, "cover") : "none";
            static int Score(string cover) => cover == "full" ? 2 : cover == "half" ? 1 : 0;
            return Score(structure) > Score(terrain) ? structure : terrain;
        }
    }

    public string DirectionalCoverFrom(ITacticalGrid grid, Coord attackerAt) =>
        battlefield.DirectionalCoverAt(At)[grid.Toward(At, attackerAt)] is { } cover
            ? StateJson.Text(cover, "directional cover") : "none";
}
