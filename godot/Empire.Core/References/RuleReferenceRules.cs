using Empire.Core.Registries;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.References;

/// <summary>The actual owners consulted by a rule reference check.</summary>
public sealed record RuleReferenceRules
{
    public required ReadOnlyContent Content
    {
        get; init;
    }
    public required GridRegistry Grids
    {
        get; init;
    }
    public required IKeyedRegistryView Abilities
    {
        get; init;
    }
    public required WeaponHitEffectHandlerRegistry HitEffects
    {
        get; init;
    }
    public required IRuleTreeRegistry Objectives
    {
        get; init;
    }
    public required IRuleTreeRegistry ScenarioConditions
    {
        get; init;
    }
    public required IRulePayloadRegistry ScenarioEffects
    {
        get; init;
    }
    public required IKeyedRegistryView Reactions
    {
        get; init;
    }
    public required IKeyedRegistryView TurnOrders
    {
        get; init;
    }
    public required IKeyedRegistryView AreaShapes
    {
        get; init;
    }
    public required IKeyedRegistryView Directives
    {
        get; init;
    }
}
