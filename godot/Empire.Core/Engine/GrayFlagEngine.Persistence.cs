using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Deployment;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Persistence;
using Empire.Core.Resources;
using Empire.Core.References;
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

    private void ComposePersistence(IReadOnlyDictionary<string, double> pluginManifest)
    {
        persistenceManifest = Manifest.Create(Catalog, pluginManifest);
        var references = new RuleReferenceRules
        {
            Content = Catalog,
            Grids = Grids,
            Abilities = Abilities,
            HitEffects = Combat.HitEffects,
            Objectives = Objectives,
            ScenarioConditions = ScenarioConditions,
            ScenarioEffects = ScenarioEffects,
            Reactions = Reactions,
            TurnOrders = TurnOrders,
            AreaShapes = AreaShapes,
            Directives = Directives,
        };
        var structuralGuards = new BattleSaveFeatures()
            .Guard("deployment", DeploymentStateValidation.Issues)
            .Guard("transport", Transport.StoredStateIssues);
        var environment = new BattleSaveEnvironment(references, ReferenceChecks, persistenceManifest, structuralGuards);
        Persistence = new BattlePersistence(environment, Saves);
        definitions = new BattleDefinitionValidator(references, ReferenceChecks, Grids);
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
