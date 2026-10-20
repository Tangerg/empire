using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Deployment;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Persistence;
using Empire.Core.Resources;
using Empire.Core.Scenario;
using Empire.Core.Transactions;
using Empire.Core.Validation;
using Manifest = Empire.Core.Persistence.RulesetManifest;

namespace Empire.Core.Engine;

public sealed partial class GrayFlagEngine
{
    private JsonObject persistenceManifest = null!;
    private BattleDefinitionValidator definitions = null!;
    public JsonObject RulesetManifest => (JsonObject)persistenceManifest.DeepClone();
    public BattlePersistence Persistence { get; private set; } = null!;

    private void ComposePersistence()
    {
        persistenceManifest = Manifest.Create(packs, new Dictionary<string, int>
        {
            ["engine.ai-planning"] = 1,
            ["engine.content"] = 1,
            ["engine.mission-rules"] = 1,
            ["engine.resource-economy"] = 1,
            ["engine.tactical-rules"] = 1,
        });
        var features = new BattleSaveFeatures()
            .Register("abilities", Abilities.Keys())
            .Register("reactions", Combat.Forecasts.ReactionIds)
            .Register("directives", Directives.Keys())
            .Register("turnOrders", TurnOrderSystem.SupportedPolicies)
            .Register("grids", Grids.Keys())
            .Register("objectives", VictorySystem.SupportedObjectives.Concat(CompositeRules.SupportedObjectives))
            .Register("scenarioConditions", ScenarioSystem.SupportedConditions.Concat(ScenarioExtensions.SupportedConditions))
            .Register("scenarioEffects", ScenarioSystem.SupportedEffects.Concat(ScenarioExtensions.SupportedEffects))
            .Register("areaShapes", AreaShapes.Keys())
            .Register("hitEffects", BattleCombat.SupportedHitEffects)
            .Register("castAbilities", BattleCombat.SupportedCastAbilities)
            .Register("resources", Resources.Adapters.Keys())
            .Register("tacticEffects", SupportOrders.SupportedTacticEffects)
            .Register("stateFeatures", new[] { "composites", "deployment", "embarkedUnits" })
            .Guard("deployment", DeploymentStateValidation.Issues)
            .Guard("transport", Transport.StoredStateIssues);
        Persistence = new BattlePersistence(packs, persistenceManifest, features);
        definitions = new BattleDefinitionValidator(packs, features, Grids);
        definitions.ValidateContent();
    }

    public JsonObject SaveBattle(JsonObject state, string? savedAt = null) => Persistence.Save(state, savedAt);
    public JsonObject LoadBattle(JsonNode? raw) => Persistence.Load(raw);
    public string SaveBattleJson(JsonObject state, string? savedAt = null) =>
        Empire.Core.Determinism.EcmaJson.Stringify(SaveBattle(state, savedAt));
    public JsonObject LoadBattleJson(string json) => LoadBattle(Empire.Core.Determinism.EcmaJson.Parse(json));
    public Battle LoadSavedBattle(JsonNode? raw) => RestoreBattle(LoadBattle(raw));
    public BattleRecorder RecordBattle(JsonObject initialState) => new(persistenceManifest, initialState);
    public ReplayOutcome ReplayBattle(JsonObject level, JsonNode? replay) =>
        BattleReplayVerifier.Verify(persistenceManifest, level, replay, (data, seed) => CreateBattle(data, seed));
}
