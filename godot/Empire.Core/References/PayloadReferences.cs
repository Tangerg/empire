using System.Text.Json.Nodes;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.References;

public readonly record struct PlayerObjectiveReference(double Player, string Id);

public interface IRulePayloadRegistry : IKeyedRegistryView
{
    PayloadReferences References(JsonObject payload);
}

public interface IRuleTreeRegistry : IRulePayloadRegistry
{
    IReadOnlyList<JsonObject> Children(JsonObject payload);
}

/// <summary>A handler's declarations, not an interpreter for another owner's payload.</summary>
public sealed class PayloadReferences
{
    public List<string> Zones { get; } = new();
    public List<double> Players { get; } = new();
    public List<string> Structures { get; } = new();
    public List<string> Composites { get; } = new();
    public List<PlayerObjectiveReference> Objectives { get; } = new();
    public List<string> Statuses { get; } = new();
    public List<string> Terrains { get; } = new();
    public List<string> Overlays { get; } = new();
    public List<string> UnitTypes { get; } = new();
    public List<string> Directives { get; } = new();
    public List<JsonObject> Cells { get; } = new();
    public List<JsonObject> Edges { get; } = new();
    public List<JsonObject> Conditions { get; } = new();
    public List<string> Faults { get; } = new();
    public PayloadReferences Zone(string? id)
    {
        if (id is not null)
            Zones.Add(id);
        return this;
    }
    public PayloadReferences Player(double? id)
    {
        if (id is not null)
            Players.Add(id.Value);
        return this;
    }
    public PayloadReferences Structure(string? id)
    {
        if (id is not null)
            Structures.Add(id);
        return this;
    }
    public PayloadReferences Composite(string? id)
    {
        if (id is not null)
            Composites.Add(id);
        return this;
    }
    public PayloadReferences Objective(double player, string id)
    {
        Objectives.Add(new(player, id));
        return this;
    }
    public PayloadReferences Status(string? id)
    {
        if (id is not null)
            Statuses.Add(id);
        return this;
    }
    public PayloadReferences Terrain(string? id)
    {
        if (id is not null)
            Terrains.Add(id);
        return this;
    }
    public PayloadReferences Overlay(string? id)
    {
        if (id is not null)
            Overlays.Add(id);
        return this;
    }
    public PayloadReferences UnitType(string? id)
    {
        if (id is not null)
            UnitTypes.Add(id);
        return this;
    }
    public PayloadReferences Directive(string? id)
    {
        if (id is not null)
            Directives.Add(id);
        return this;
    }
    public PayloadReferences Cell(JsonObject? at)
    {
        if (at is not null)
            Cells.Add(at);
        return this;
    }
    public PayloadReferences Edge(JsonObject edge)
    {
        Edges.Add(edge);
        return this;
    }
    public PayloadReferences Condition(JsonObject condition)
    {
        Conditions.Add(condition);
        return this;
    }
    public PayloadReferences Fault(string message)
    {
        Faults.Add(message);
        return this;
    }
    public PayloadReferences Selector(JsonObject selector) => Zone(selector["zone"] is { } zone ? StateJson.Text(zone, "zone") : null);
}
