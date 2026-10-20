using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Terrain;

public sealed record ElevationChange(double From, double To);

/// <summary>Canonical writable terrain, elevation, cliff and directional-cover layers.</summary>
public sealed class MapLayers
{
    private readonly JsonObject map;
    private readonly GridExtent extent;
    public MapLayers(JsonObject map)
    { this.map = map; extent = new GridExtent(Integer(map, "width"), Integer(map, "height")); }
    public bool Contains(Coord at) => GridStorage.InBounds(extent, at.X, at.Y);
    private int Index(Coord at) => Contains(at) ? GridStorage.Index(extent, at.X, at.Y)
        : throw new ArgumentOutOfRangeException(null, $"cell ({at.X}, {at.Y}) is outside the map");
    public string? ChangeTerrain(Coord at, string terrain)
    {
        var index = Index(at);
        var tiles = Array(map["tiles"], "tiles");
        var previous = Text(tiles[index], "terrain");
        if (previous == terrain) return null;
        tiles[index] = terrain;
        return previous;
    }
    public double ElevationAt(Coord at) => Array(map["elevation"], "elevation")[Index(at)] is { } value ? Number(value, "elevation") : 0;
    public ElevationChange? ChangeElevation(Coord at, double value)
    {
        var index = Index(at);
        var from = ElevationAt(at);
        var to = Round(value);
        if (from == to) return null;
        Array(map["elevation"], "elevation")[index] = to;
        return new(from, to);
    }
    public ElevationChange? RaiseElevation(Coord at, double amount) => ChangeElevation(at, ElevationAt(at) + Round(amount));
    public bool IsEdge(Coord from, Coord to) => Contains(from) && Contains(to) && GridStorage.SharesEdge(from, to);
    public bool BlockEdge(Coord from, Coord to, bool blocked)
    {
        if (!IsEdge(from, to)) throw new ArgumentOutOfRangeException(null, $"not an orthogonal map edge: {from.X},{from.Y} -> {to.X},{to.Y}");
        var cliffs = Array(map["cliffs"], "cliffs");
        var key = GridStorage.EdgeKey(from, to);
        var existing = Objects(cliffs, "cliffs").FirstOrDefault(edge => GridStorage.EdgeKey(
            TacticalQueries.Position(Object(edge["from"], "cliff from")), TacticalQueries.Position(Object(edge["to"], "cliff to"))) == key);
        if (blocked == (existing is not null)) return false;
        if (blocked) cliffs.Add(new JsonObject { ["from"] = TacticalQueries.CoordinateJson(from), ["to"] = TacticalQueries.CoordinateJson(to) });
        else cliffs.Remove(existing);
        return true;
    }
    public void ChangeCover(Coord at, JsonObject sides)
    {
        Index(at);
        var covers = Array(map["directionalCover"], "directionalCover");
        var entry = Objects(covers, "directionalCover").FirstOrDefault(cover => TacticalQueries.Position(Object(cover["at"], "cover at")) == at);
        if (entry is null) covers.Add(new JsonObject { ["at"] = TacticalQueries.CoordinateJson(at), ["sides"] = sides.DeepClone() });
        else entry["sides"] = sides.DeepClone();
        foreach (var empty in Objects(covers, "directionalCover").Where(cover => Object(cover["sides"], "cover sides").Count == 0).ToArray()) covers.Remove(empty);
    }
}
