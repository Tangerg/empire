using Empire.Core.Abilities;
using Empire.Core.Actions;
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
using Empire.Core.Statuses;
using Empire.Core.State;
using Empire.Core.Tactics;
using Keys = Empire.Core.Composition.BattleCapabilities;

namespace Empire.Core.Composition;

public static class DefaultBattlePlugins
{
    public static EnginePlugin ContentFor(ReadOnlyContent content) => new("engine.content", 1,
        context => context.Provide(Keys.Content, content), provides: [Keys.Content]);

    public static EnginePlugin TacticalRules
    {
        get;
    } = new("engine.tactical-rules", 2, context =>
    {
        context.Provide(Keys.Grids, GridRegistry.Core());
        context.Provide(Keys.Abilities, DefaultAbilities.Create());
        context.Provide(Keys.CombatModifiers, DefaultCombatModifiers.Create());
        context.Provide(Keys.HitEffects, DefaultWeaponHitEffects.Create());
        context.Provide(Keys.StatusBehaviors, new StatusBehaviorRegistry());
        context.Provide(Keys.Progression, new ThresholdRankProgressionPolicy());
        context.Provide(Keys.TurnOrders, TurnOrderRegistry.Core());
        context.Provide(Keys.Reactions, ReactionRegistry.Core());
        context.Provide(Keys.UnitDepartures, DefaultUnitDepartureHandlers.Create());
        context.Provide(Keys.Random, new DefaultBattleRandomSource());
        context.Provide(Keys.AreaShapes, WeaponAreaRegistry.Core());
        context.Provide(Keys.Directives, UnitDirectiveRegistry.Core());
        context.Provide(Keys.ReferenceChecks, BattleReferenceCheckRegistry.CreateDefault());
        context.Provide(Keys.Saves, new BattleSaveReader());
    }, provides: [Keys.Abilities, Keys.CombatModifiers, Keys.HitEffects, Keys.StatusBehaviors,
        Keys.Progression, Keys.TurnOrders, Keys.Reactions, Keys.UnitDepartures, Keys.Random,
        Keys.AreaShapes, Keys.Directives, Keys.ReferenceChecks, Keys.Saves, Keys.Grids]);

    public static EnginePlugin TacticalSpace
    {
        get;
    } = new("engine.tactical-space", 1, context =>
        context.Provide(Keys.Space, new DefaultTacticalSpace(new TacticalSpaceRules(context.Require(Keys.Content), context.Require(Keys.Grids)))),
        provides: [Keys.Space], requiresCapabilities: [Keys.Content, Keys.Grids]);

    public static EnginePlugin MissionRules
    {
        get;
    } = new("engine.mission-rules", 2, context =>
    {
        context.Provide(Keys.ActionHandlers, DefaultActionHandlers.Create());
        context.Provide(Keys.ScenarioConditions, DefaultScenarioConditions.Create(context.Require(Keys.Random)));
        context.Provide(Keys.ScenarioEffects, DefaultScenarioEffects.Create());
        context.Provide(Keys.Objectives, DefaultObjectives.Create());
    }, provides: [Keys.ActionHandlers, Keys.ScenarioConditions, Keys.ScenarioEffects, Keys.Objectives],
        requiresCapabilities: [Keys.Random]);

    public static EnginePlugin ResourceEconomy
    {
        get;
    } = new("engine.resource-economy", 1,
        context => context.Provide(Keys.Resources, new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone()))), provides: [Keys.Resources]);

    public static EnginePlugin AiPlanning
    {
        get;
    } = new("engine.ai-planning", 1, context =>
    {
        context.Provide(Keys.AiObjectiveAdvisors, DefaultAiObjectiveAdvisors.Create());
        context.Provide(Keys.AbilityAiEvaluators, DefaultAbilityAiEvaluators.Create());
        context.Provide(Keys.AiIntents, DefaultAiIntents.Create());
    }, provides: [Keys.AiObjectiveAdvisors, Keys.AbilityAiEvaluators, Keys.AiIntents],
        requiresCapabilities: [Keys.Content, Keys.Abilities, Keys.Space, Keys.Objectives, Keys.Resources]);

    public static IReadOnlyList<EnginePlugin> Rules
    {
        get;
    } = Array.AsReadOnly(new[]
        { TacticalRules, TacticalSpace, MissionRules, ResourceEconomy, AiPlanning });
}
