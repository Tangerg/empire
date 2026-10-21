using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Keys = Empire.Core.Composition.BattleCapabilities;

var root = Path.GetFullPath(args.Length == 0 ? "../../.." : args[0]);
var source = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
var level = EcmaJson.Parse(File.ReadAllText(Path.Combine(root,
    "godot/data/grayflag-campaign-levels.json")))!.AsArray().OfType<JsonObject>()
    .Single(item => item["id"]!.GetValue<string>() == "c01-01");
if (args.Contains("--benchmark-only", StringComparer.Ordinal))
{
    CatalogBenchmark.Run(root);
    return;
}
var checks = 0;
void Check(bool value, string label)
{
    checks++;
    if (!value)
        throw new InvalidOperationException(label);
}
void Equal(JsonNode? actual, JsonNode? expected, string label) => Check(EcmaJson.DeepEquals(actual, expected), label);
JsonObject Entry(string family, string id) => source.OfType<JsonObject>()
    .SelectMany(pack => pack[family]?.AsArray().OfType<JsonObject>() ?? [])
    .Single(value => value["id"]!.GetValue<string>() == id);

const string weaponId = "c01.border-blade";
var authoredWeapon = Entry("weapons", weaponId);
var statusId = source.OfType<JsonObject>().SelectMany(pack => pack["statuses"]?.AsArray().OfType<JsonObject>() ?? [])
    .First()["id"]!.GetValue<string>();
authoredWeapon["power"] = 1;
authoredWeapon["hitEffects"] = new JsonArray(new JsonObject
{
    ["type"] = "addStatus",
    ["status"] = statusId,
    ["duration"] = 2,
    ["probe"] = new JsonObject { ["values"] = new JsonArray("original") },
});
authoredWeapon["utf16Probe"] = new JsonObject { ["\ud800"] = "\udc00" };
var catalog = new ReadOnlyContent(source);
var canonical = catalog.Definition("weapons", weaponId)!.Value.GetRawText();
var catalogBefore = catalog.Families.ToDictionary(family => family,
    family => string.Join("\n", catalog.Definitions(family).Select(value => value.GetRawText())));
var immutableDefinitions = (IList<JsonElement>)catalog.Definitions("weapons");
try
{
    immutableDefinitions.Clear();
    throw new InvalidOperationException("Definition collections must reject mutation.");
}
catch (NotSupportedException) { Check(true, "definition collections are immutable"); }
authoredWeapon["power"] = 98765;
authoredWeapon["hitEffects"]![0]!["probe"]!["values"]![0] = "changed input";
Check(catalog.Definition("weapons", weaponId)!.Value.GetRawText() == canonical, "authored packs cannot mutate the catalog");
Check(EcmaJson.TryGetProperty(catalog.Definition("weapons", weaponId)!.Value.GetProperty("utf16Probe"), "\ud800", out var rawUtf16)
    && rawUtf16.GetRawText() == "\"\\udc00\"", "immutable UTF-16 property lookup");
var utf16 = EcmaJson.Parse(canonical)!["utf16Probe"]!.AsObject();
Check(utf16.ContainsKey("\ud800") && EcmaJson.ReadString(utf16["\ud800"]) == "\udc00", "immutable values retain exact UTF-16 code units");

JsonObject? callbackWeapon = null;
JsonObject? callbackEffect = null;
var plugin = new EnginePlugin("test.catalog-boundaries", 1, context =>
{
    context.Replace(Keys.CombatModifiers, DefaultCombatModifiers.Create().Register(new("test.observe-weapon", 1000, query =>
    {
        callbackWeapon = query.Weapon;
        return [];
    })));
    var effects = context.Require(Keys.HitEffects).Clone();
    var original = effects.Get("addStatus");
    effects.Replace(original with
    {
        Apply = (query, effect) =>
    {
        callbackEffect = effect;
        original.Apply(query, effect);
    }
    });
    context.Replace(Keys.HitEffects, effects);
}, overrides: [Keys.CombatModifiers, Keys.HitEffects], requiresCapabilities: [Keys.HitEffects]);
var engine = BattleEngineFactory.Create(catalog, [plugin]);
var peer = BattleEngineFactory.Create(catalog, [plugin]);
Check(ReferenceEquals(engine.Catalog, peer.Catalog) && ReferenceEquals(engine.Catalog, catalog), "two engines share the canonical catalog");
Check(ReferenceEquals(((DefaultTacticalSpace)engine.Space).Content, catalog), "spatial rules consume the composed catalog");
var state = engine.CreateState(level, 2468);
var actor = state["units"]!.AsArray().OfType<JsonObject>()
    .First(unit => engine.Combat.Forecasts.UnitWeapons(unit).Any(weapon => weapon["id"]!.GetValue<string>() == weaponId));
var defender = state["units"]!.AsArray().OfType<JsonObject>()
    .First(unit => unit["owner"]!.GetValue<int>() != actor["owner"]!.GetValue<int>());
var initialHash = StateDigest.Hash(state);
var initialSave = engine.SaveBattleJson(state, "2026-10-21T00:00:00.000Z");
var originalDamage = engine.Combat.Forecasts.ComputeDamage(state, actor, defender, weapon: weaponId);

void CorruptWeapon(JsonObject weapon, string surface)
{
    weapon["power"] = 98765;
    weapon["tags"]!.AsArray().Clear();
    if (weapon["id"]!.GetValue<string>() == weaponId)
    {
        Check(EcmaJson.ReadString(weapon["utf16Probe"]!["\ud800"]) == "\udc00", surface + " exact UTF-16 clone");
        weapon["hitEffects"]![0]!["probe"]!["values"]![0] = "corrupted " + surface;
    }
    Check(catalog.Definition("weapons", weaponId)!.Value.GetRawText() == canonical, surface + " cannot change canonical definition");
}
CorruptWeapon(engine.Combat.Forecasts.UnitWeapons(actor).Single(weapon => weapon["id"]!.GetValue<string>() == weaponId), "UnitWeapons");
CorruptWeapon(engine.Combat.Forecasts.PrimaryWeapon(actor), "PrimaryWeapon");
CorruptWeapon(engine.Combat.Forecasts.ReadyWeapon(state, actor, weaponId)!, "ReadyWeapon");
CorruptWeapon(engine.Combat.Forecasts.RequireReadyWeapon(state, actor, weaponId), "RequireReadyWeapon");
CorruptWeapon(engine.Combat.SelectedWeapon(state, actor, weaponId)!, "SelectedWeapon");
var ability = engine.Abilities.Get("attack");
var query = new AbilityQuery(state, actor, TacticalQueries.Position(actor), false, weaponId);
CorruptWeapon(ability.WeaponFor(engine.AbilityRules, query)!, "AbilityDefinition.WeaponFor");
CorruptWeapon(ability.WeaponChoices(engine.AbilityRules, query).Single(weapon => weapon["id"]!.GetValue<string>() == weaponId), "AbilityDefinition.WeaponChoices");
var reactive = engine.Combat.Forecasts.BestReactiveStrike(state, actor, new(1, 1), defender, new(2, 1), engine.Reactions.Get("counter"));
Check(reactive is not null, "reactive fixture offers a strike");
CorruptWeapon(reactive!.Weapon, "ReactiveStrike");
CorruptWeapon(callbackWeapon!, "CombatModifierProvider callback");
Check(engine.Combat.Forecasts.ComputeDamage(state, actor, defender, weapon: weaponId).Damage == originalDamage.Damage,
    "later forecasts retain canonical weapon power");

var plan = engine.Combat.Forecast(state, actor, TacticalQueries.Position(defender), weapon: weaponId);
var hit = plan.UnitHits.First(value => value.Effects.Count > 0);
hit.Effects[0]["probe"]!["values"]![0] = "changed plan";
var nextPlan = peer.Combat.Forecast(state, actor, TacticalQueries.Position(defender), weapon: weaponId);
Check(nextPlan.UnitHits.First(value => value.Effects.Count > 0).Effects[0]["probe"]!["values"]![0]!.GetValue<string>() == "original",
    "plans from separate engines isolate nested effects");
var effectState = state.DeepClone().AsObject();
engine.Combat.ApplyHitEffects(effectState, effectState["units"]![0]!.AsObject(), effectState["units"]![1]!.AsObject(),
    nextPlan.UnitHits.First(value => value.Effects.Count > 0).Effects, _ => { });
callbackEffect!["probe"]!["values"]![0] = "changed callback";
Check(catalog.Definition("weapons", weaponId)!.Value.GetRawText() == canonical, "hit-effect callbacks cannot mutate catalog payloads");

var utf16Catalog = new ReadOnlyContent(new JsonArray(new JsonObject
{
    ["id"] = "test.utf16-keys",
    ["version"] = 1,
    ["movementProfiles"] = new JsonArray(new JsonObject { ["id"] = "\ud800", ["tags"] = new JsonArray() }),
    ["terrains"] = new JsonArray(new JsonObject
    {
        ["id"] = "test.terrain",
        ["cost"] = new JsonObject { ["\ud800"] = 1 },
    }),
}));
var utf16State = state.DeepClone().AsObject();
utf16State["map"]!["tiles"]![0] = "test.terrain";
Check(new MovementRules(new MovementRuleServices(utf16Catalog, engine.Grids)).Battlefield(utf16State).Cell(new(0, 0)).MovementCost("\ud800") == 1,
    "immutable movement reads preserve lone-surrogate content keys");

var movement = new MovementRules(new MovementRuleServices(catalog, engine.Grids));
var cell = movement.Battlefield(state).Cell(TacticalQueries.Position(actor));
var terrain = cell.Terrain;
var terrainId = terrain.GetProperty("id").GetString()!;
var terrainCopy = EcmaJson.Parse(terrain.GetRawText())!.AsObject();
terrainCopy["cost"]!.AsObject().Clear();
terrainCopy["defense"] = 98765;
Check(cell.Terrain.GetRawText() == catalog.Definition("terrains", terrainId)!.Value.GetRawText(), "terrain is an immutable nested value");
var overlayState = state.DeepClone().AsObject();
overlayState["scenario"]!["overlays"]!.AsArray().Add(new JsonObject
{
    ["id"] = "test.overlay",
    ["type"] = "flooded",
    ["cells"] = new JsonArray(TacticalQueries.CoordinateJson(TacticalQueries.Position(actor))),
    ["remainingRounds"] = null,
});
var overlayCell = movement.Battlefield(overlayState).Cell(TacticalQueries.Position(actor));
var overlayCopy = EcmaJson.Parse(overlayCell.Overlays.Single().GetRawText())!.AsObject();
overlayCopy["blockedMovementClasses"]!.AsArray().Clear();
Check(overlayCell.Overlays.Single().GetProperty("blockedMovementClasses").GetArrayLength() > 0, "overlays preserve immutable nested collections");

var formationState = state.DeepClone().AsObject();
var formationActor = formationState["units"]!.AsArray().OfType<JsonObject>().First(unit => unit["id"]!.GetValue<int>() == actor["id"]!.GetValue<int>());
var ally = formationState["units"]!.AsArray().OfType<JsonObject>().First(unit => unit != formationActor && unit["owner"]!.GetValue<int>() == formationActor["owner"]!.GetValue<int>());
formationActor["x"] = 1;
formationActor["y"] = 1;
formationActor["formation"] = "formation-line";
ally["x"] = 1;
ally["y"] = 2;
foreach (var definition in new[]
{
    engine.Progression.Formations.InEffect(formationState, formationActor, "formation-line"),
    engine.Progression.Formations.Active(formationState, formationActor),
})
{
    Check(definition is not null, "formation fixture is active");
    definition!["attackMultiplier"] = 98765;
    definition["tags"]!.AsArray().Clear();
}
Check(engine.Progression.Formations.Active(formationState, formationActor)!["attackMultiplier"]!.GetValue<double>() == 1.05,
    "formation queries isolate definitions and nested tags");
Check(StateDigest.Hash(state) == initialHash, "definition queries do not mutate battle state");
Check(engine.SaveBattleJson(state, "2026-10-21T00:00:00.000Z") == initialSave, "save bytes remain identical after queries");

var battle = engine.CreateBattle(level, 2468);
var control = peer.CreateBattle(level, 2468);
var recorder = engine.RecordBattle(battle.Snapshot());
for (var turn = 0; turn < 12 && battle.Snapshot()["phase"]!.GetValue<string>() != "over"; turn++)
{
    var before = battle.Snapshot();
    var action = engine.Ai.ChooseAction(before, 0.58);
    var actual = battle.Dispatch(action);
    var expected = control.Dispatch(action);
    recorder.Record(action);
    Equal(new JsonArray(actual.Events.Select(value => value.DeepClone()).ToArray()),
        new JsonArray(expected.Events.Select(value => value.DeepClone()).ToArray()), "post-query events retain exact deterministic order");
    Check(actual.StateHash == expected.StateHash, "post-query state hashes match the untouched engine");
}
var saved = engine.SaveBattle(battle.Snapshot(), "2026-10-21T00:00:00.000Z");
Equal(peer.LoadBattle(saved), battle.Snapshot(), "post-query save loads through the shared catalog");
var replay = peer.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
Check(replay.DivergedAt is null, "post-query replay has no divergence");
Equal(replay.State, battle.Snapshot(), "post-query replay ends in the exact state");
Check(catalog.Definition("weapons", weaponId)!.Value.GetRawText() == canonical, "all consumer paths preserve catalog identity");
foreach (var family in catalog.Families)
    Check(string.Join("\n", catalog.Definitions(family).Select(value => value.GetRawText())) == catalogBefore[family],
        "all consumer paths preserve the complete " + family + " catalog");
Console.WriteLine($"PASS {checks} external ProjectReference catalog ownership assertions");
if (args.Contains("--benchmark", StringComparer.Ordinal)) CatalogBenchmark.Run(root);
