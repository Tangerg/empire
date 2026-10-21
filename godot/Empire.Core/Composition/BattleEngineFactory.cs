using Empire.Core.Engine;
using Empire.Core.State;
using Keys = Empire.Core.Composition.BattleCapabilities;

namespace Empire.Core.Composition;

public static class BattleEngineFactory
{
    public static BattleEngine Create(ReadOnlyContent content, IEnumerable<EnginePlugin>? plugins = null) =>
        new(Compose(content, plugins));

    public static EnginePlugin OverridePlugin<T>(CapabilityKey<T> capability, T value) => new(
        Empire.Core.Persistence.RulesetManifest.AnonymousOverrideId(capability.Id), 1, context => context.Replace(capability, value), overrides: [capability]);

    internal static BattleEngineDependencies Compose(ReadOnlyContent content, IEnumerable<EnginePlugin>? plugins = null)
    {
        var kernel = new SrpgMicrokernel().Use(DefaultBattlePlugins.ContentFor(content)).UseAll(DefaultBattlePlugins.Rules);
        if (plugins is not null)
            kernel.UseAll(plugins);
        var capabilities = kernel.Compose();
        var dependencies = new BattleEngineDependencies
        {
            Content = capabilities.Require(Keys.Content),
            Abilities = capabilities.Require(Keys.Abilities),
            Space = capabilities.Require(Keys.Space),
            ActionHandlers = capabilities.Require(Keys.ActionHandlers),
            CombatModifiers = capabilities.Require(Keys.CombatModifiers),
            HitEffects = capabilities.Require(Keys.HitEffects),
            StatusBehaviors = capabilities.Require(Keys.StatusBehaviors),
            Progression = capabilities.Require(Keys.Progression),
            TurnOrders = capabilities.Require(Keys.TurnOrders),
            Reactions = capabilities.Require(Keys.Reactions),
            UnitDepartures = capabilities.Require(Keys.UnitDepartures),
            Random = capabilities.Require(Keys.Random),
            AreaShapes = capabilities.Require(Keys.AreaShapes),
            Directives = capabilities.Require(Keys.Directives),
            ReferenceChecks = capabilities.Require(Keys.ReferenceChecks),
            Saves = capabilities.Require(Keys.Saves),
            Grids = capabilities.Require(Keys.Grids),
            ScenarioConditions = capabilities.Require(Keys.ScenarioConditions),
            ScenarioEffects = capabilities.Require(Keys.ScenarioEffects),
            Objectives = capabilities.Require(Keys.Objectives),
            Resources = capabilities.Require(Keys.Resources),
            AiObjectiveAdvisors = capabilities.Require(Keys.AiObjectiveAdvisors),
            AbilityAiEvaluators = capabilities.Require(Keys.AbilityAiEvaluators),
            AiIntents = capabilities.Require(Keys.AiIntents),
            PluginManifest = capabilities.PluginManifest,
        };
        dependencies.Abilities.Seal();
        dependencies.ActionHandlers.Seal();
        dependencies.CombatModifiers.Seal();
        dependencies.HitEffects.Seal();
        dependencies.AreaShapes.Seal();
        dependencies.Directives.Seal();
        dependencies.Grids.Seal();
        dependencies.Resources.Seal();
        dependencies.AiObjectiveAdvisors.Seal();
        dependencies.AbilityAiEvaluators.Seal();
        dependencies.AiIntents.Seal();
        dependencies.StatusBehaviors.Seal();
        dependencies.TurnOrders.Seal();
        dependencies.Reactions.Seal();
        dependencies.UnitDepartures.Seal();
        dependencies.ReferenceChecks.Seal();
        dependencies.ScenarioConditions.Seal();
        dependencies.ScenarioEffects.Seal();
        dependencies.Objectives.Seal();
        return dependencies;
    }
}
