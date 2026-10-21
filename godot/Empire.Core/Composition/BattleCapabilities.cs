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

namespace Empire.Core.Composition;

public static class BattleCapabilities
{
    public static CapabilityKey<ReadOnlyContent> Content { get; } = new("content");
    public static CapabilityKey<AbilityRegistry> Abilities { get; } = new("abilities");
    public static CapabilityKey<ITacticalSpace> Space { get; } = new("space");
    public static CapabilityKey<BattleActionRegistry> ActionHandlers { get; } = new("actionHandlers");
    public static CapabilityKey<CombatModifierPipeline> CombatModifiers { get; } = new("combatModifiers");
    public static CapabilityKey<WeaponHitEffectHandlerRegistry> HitEffects { get; } = new("hitEffects");
    public static CapabilityKey<StatusBehaviorRegistry> StatusBehaviors { get; } = new("statusBehaviors");
    public static CapabilityKey<IRankProgressionPolicy> Progression { get; } = new("progression");
    public static CapabilityKey<TurnOrderRegistry> TurnOrders { get; } = new("turnOrders");
    public static CapabilityKey<ReactionRegistry> Reactions { get; } = new("reactions");
    public static CapabilityKey<UnitDepartureHandlerRegistry> UnitDepartures { get; } = new("unitDepartures");
    public static CapabilityKey<IBattleRandomSource> Random { get; } = new("random");
    public static CapabilityKey<WeaponAreaRegistry> AreaShapes { get; } = new("areaShapes");
    public static CapabilityKey<UnitDirectiveRegistry> Directives { get; } = new("directives");
    public static CapabilityKey<BattleReferenceCheckRegistry> ReferenceChecks { get; } = new("referenceChecks");
    public static CapabilityKey<IBattleSaveReader> Saves { get; } = new("saves");
    public static CapabilityKey<GridRegistry> Grids { get; } = new("grids");
    public static CapabilityKey<ScenarioConditionHandlerRegistry> ScenarioConditions { get; } = new("scenarioConditions");
    public static CapabilityKey<ScenarioEffectHandlerRegistry> ScenarioEffects { get; } = new("scenarioEffects");
    public static CapabilityKey<ObjectiveHandlerRegistry> Objectives { get; } = new("objectives");
    public static CapabilityKey<BattleResources> Resources { get; } = new("resources");
    public static CapabilityKey<AiObjectiveAdvisorRegistry> AiObjectiveAdvisors { get; } = new("aiObjectiveAdvisors");
    public static CapabilityKey<AbilityAiEvaluatorRegistry> AbilityAiEvaluators { get; } = new("abilityAiEvaluators");
    public static CapabilityKey<AiIntentRegistry> AiIntents { get; } = new("aiIntents");
}
