using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Orders;
using Empire.Core.Progression;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Abilities;

public sealed record CommandOption(string Key, string Ability, string? Weapon, string Name,
    string Hint, bool SelfTargeted, IReadOnlyList<Coord> Targets, string? Engagement = null);

public sealed record AbilityQuery(JsonObject State, JsonObject Unit, Coord At, bool Moved, string? WeaponId = null);

/// <summary>The canonical collaborators read or invoked by ability definitions and command policy.</summary>
public sealed record AbilityRules(ReadOnlyContent Content, MovementRules Space, BattleCombat Combat,
    SupportOrders Support, CareerRules Careers, AbilityRegistry Abilities);

public sealed record AbilityDefinition(string Id, string Name)
{
    private readonly IReadOnlyList<string> tags = Array.Empty<string>();
    public string Hint { get; init; } = "";
    public bool SelfTargeted { get; init; } = true;
    public double Priority { get; init; } = 50;
    public IReadOnlyList<string> Tags { get => tags; init => tags = Array.AsReadOnly(value.ToArray()); }
    public string? Engagement { get; init; }
    public Func<AbilityRules, AbilityQuery, JsonObject?> WeaponFor { get; init; } = (_, _) => null;
    public Func<AbilityRules, AbilityQuery, IReadOnlyList<JsonObject>> WeaponChoices { get; init; } = (_, _) => [];
    public Func<AbilityRules, AbilityQuery, IReadOnlyList<Coord>> Targets { get; init; } = (_, _) => [];
    public Func<AbilityRules, AbilityQuery, bool> Usable { get; init; } = (_, _) => true;
    public Action<AbilityRules, AbilityQuery, Coord?, Action<JsonObject>> Execute { get; init; } = (_, _, _, _) => { };
}
