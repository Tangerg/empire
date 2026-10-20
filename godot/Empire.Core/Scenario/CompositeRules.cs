using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Scenario;

public sealed record CompositeStatus(int Neutralized, string State);

/// <summary>Composite status and atomic translation share the same surviving-part semantics.</summary>
public static class CompositeRules
{
    public static IReadOnlyList<string> SupportedObjectives { get; } =
        System.Array.AsReadOnly(new[] { "neutralizeComposite" });

    private static JsonObject Require(JsonObject state, string id) => Objects(state["composites"], "composites")
        .FirstOrDefault(composite => Text(composite, "id") == id) ?? throw new DomainInvariantException($"unknown composite \"{id}\"");

    public static CompositeStatus Status(JsonObject state, string id)
    {
        var composite = Require(state, id);
        var structures = Objects(state["structures"], "structures").ToArray();
        var neutralized = Array(composite["parts"], "parts").Count(part =>
        {
            var structure = structures.FirstOrDefault(value => Text(value, "id") == Text(part, "part"));
            return structure is null || Number(structure, "hp", 0) <= 0 || IsTrue(structure["disabled"]);
        });
        return new(neutralized, neutralized >= Number(composite["minimumNeutralized"], "minimumNeutralized")
            ? "neutralized" : neutralized > 0 ? "damaged" : "intact");
    }

    public static string ObjectiveOutcome(JsonObject state, int owner, JsonObject objective)
    {
        if (Text(objective, "type") != "neutralizeComposite")
            throw new DomainInvariantException($"unknown objective handler \"{Text(objective, "type")}\"");
        var id = Text(objective, "composite");
        var composite = Require(state, id);
        var threshold = objective["minimumNeutralized"] ?? composite["minimumNeutralized"];
        return Status(state, id).Neutralized >= Number(threshold, "minimumNeutralized") ? "success" : "pending";
    }

    public static string ObjectiveProgress(JsonObject state, int owner, JsonObject objective)
    {
        if (Text(objective, "type") != "neutralizeComposite")
            throw new DomainInvariantException($"unknown objective handler \"{Text(objective, "type")}\"");
        var id = Text(objective, "composite");
        var composite = Require(state, id);
        return $"{Status(state, id).Neutralized}/{Number(objective["minimumNeutralized"] ?? composite["minimumNeutralized"], "minimumNeutralized")} 部件";
    }

    public static string DescribeObjective(JsonObject objective) => Text(objective, "type") == "neutralizeComposite"
        ? "瘫痪复合战场目标" : throw new DomainInvariantException($"unknown objective handler \"{Text(objective, "type")}\"");

    public static void Move(JsonObject state, string id, double dx, double dy, Action<JsonObject> emit)
    {
        var composite = Require(state, id);
        var ids = Array(composite["parts"], "parts").Select(part => Text(part, "part")).ToHashSet(StringComparer.Ordinal);
        var moving = Objects(state["structures"], "structures").Where(part => ids.Contains(Text(part, "id")) && Number(part, "hp", 0) > 0).ToArray();
        var movingIds = moving.Select(part => Text(part, "id")).ToHashSet(StringComparer.Ordinal);
        var destinations = moving.Select(part => new
        {
            Part = part,
            From = TacticalQueries.Position(part),
            X = Number(part, "x", 0) + Round(dx),
            Y = Number(part, "y", 0) + Round(dy),
        }).ToArray();
        var map = Object(state["map"], "map");
        foreach (var destination in destinations)
        {
            if (!(destination.X >= 0 && destination.Y >= 0 && destination.X < Integer(map, "width") && destination.Y < Integer(map, "height")))
                throw new DomainInvariantException($"composite \"{id}\" would move out of bounds");
            var to = new Coord((int)destination.X, (int)destination.Y);
            if (TacticalQueries.UnitAt(state, to) is not null)
                throw new DomainInvariantException($"composite \"{id}\" would collide with a unit");
            var occupied = TacticalQueries.StructureAt(state, to);
            if (occupied is not null && !movingIds.Contains(Text(occupied, "id")))
                throw new DomainInvariantException($"composite \"{id}\" would collide with structure \"{Text(occupied, "id")}\"");
        }
        if (destinations.Select(destination => (destination.X, destination.Y)).Distinct().Count() != destinations.Length)
            throw new DomainInvariantException($"composite \"{id}\" has overlapping destination parts");
        foreach (var destination in destinations)
        {
            var to = new Coord((int)destination.X, (int)destination.Y);
            destination.Part["x"] = to.X;
            destination.Part["y"] = to.Y;
            emit(new JsonObject { ["type"] = "structureMoved", ["structure"] = destination.Part["id"]!.DeepClone(),
                ["from"] = TacticalQueries.CoordinateJson(destination.From), ["to"] = TacticalQueries.CoordinateJson(to) });
        }
    }
}
