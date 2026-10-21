using Empire.Core.Abilities;
using Empire.Core.Ai;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
using Empire.Core.Persistence;
using Empire.Core.Progression;
using Empire.Core.References;
using Empire.Core.Resources;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Core.Engine;

internal sealed record BattleEngineDependencies
{
    public required ReadOnlyContent Content { get; init; }
    public required AbilityRegistry Abilities { get; init; }
    public required ITacticalSpace Space { get; init; }
    public required BattleActionRegistry ActionHandlers { get; init; }
    public required CombatModifierPipeline CombatModifiers { get; init; }
    public required WeaponHitEffectHandlerRegistry HitEffects { get; init; }
    public required StatusBehaviorRegistry StatusBehaviors { get; init; }
    public required IRankProgressionPolicy Progression { get; init; }
    public required TurnOrderRegistry TurnOrders { get; init; }
    public required ReactionRegistry Reactions { get; init; }
    public required UnitDepartureHandlerRegistry UnitDepartures { get; init; }
    public required IBattleRandomSource Random { get; init; }
    public required WeaponAreaRegistry AreaShapes { get; init; }
    public required UnitDirectiveRegistry Directives { get; init; }
    public required BattleReferenceCheckRegistry ReferenceChecks { get; init; }
    public required IBattleSaveReader Saves { get; init; }
    public required GridRegistry Grids { get; init; }
    public required ScenarioConditionHandlerRegistry ScenarioConditions { get; init; }
    public required ScenarioEffectHandlerRegistry ScenarioEffects { get; init; }
    public required ObjectiveHandlerRegistry Objectives { get; init; }
    public required BattleResources Resources { get; init; }
    public required AiObjectiveAdvisorRegistry AiObjectiveAdvisors { get; init; }
    public required AbilityAiEvaluatorRegistry AbilityAiEvaluators { get; init; }
    public required AiIntentRegistry AiIntents { get; init; }
    public required IReadOnlyDictionary<string, double> PluginManifest { get; init; }
}
