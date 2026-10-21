using Empire.Presentation.Feedback;
using Empire.Checks;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.State;
using Empire.Presentation;
using Empire.Presentation.Tactical;

if (args.Length != 3) throw new ArgumentException("Usage: ReactionAdapterChecks <oracle.json.gz> <repository> <catalog.json>");
var gameText = FixtureText.Load(args[^1]);
ActionRefusalText Refusals(GrayFlagEngine value) => ActionRefusalText.CreateDefault(gameText, value.Catalog, value.Abilities).Seal();

var root = args[1];
// Historical source-v1 runtime states still require explicit semantic-schema migration.
using var source = new GZipStream(File.OpenRead(args[0]), CompressionMode.Decompress);
var fixture = JsonNode.Parse(source)!.AsObject();
var reactions = ReactionRegistry.Core();
reactions.Replace(reactions.Get("guard") with { Name = "替换防御菜单", Hint = "来自已解析注册表" });
reactions.Register(new("plugin-dodge", "自定义闪避菜单", false, 0.4, false, false, "自定义提示"));
var engine = BattleEngineFactory.Create(new ReadOnlyContent(fixture["packs"]!.AsArray()),
    [new EnginePlugin("test.reaction-adapter", 1, context => context.Replace(BattleCapabilities.Reactions, reactions), overrides: [BattleCapabilities.Reactions])]);
var battleText = FixtureBattleText.Load(root, engine, gameText);
var state = fixture["turns"]![0]!["before"]!.DeepClone().AsObject();
var session = GrayFlagSession.FromSave(engine, engine.SaveBattle(state), gameText, Refusals(engine), battleText);
var actor = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().First(unit => engine.Turns.MayAct(state, unit));
var id = actor["id"]!.GetValue<int>();
var before = EcmaJson.Stringify(session.Snapshot());
void Require(bool result, string message) { if (!result) throw new Exception(message); }
var queries = session.GetTacticalQueries(7);
var controller = new TacticalSelectionController(queries);
controller.Click(controller.Stamp, queries.Unit(id)!.At);
var hud = session.ProjectHud(controller.Stamp, controller.Selection, controller.Cursor, controller.InspectId, false);
var rows = hud.Sections.Single(section => section.Key == "stances").Rows.Where(row => row.Key.StartsWith("reaction:", StringComparison.Ordinal)).ToArray();
Require(rows.Select(row => row.Key).SequenceEqual(reactions.Keys().Select(stance => "reaction:" + stance)), "resolved reaction HUD IDs");
Require(rows.Select(row => row.Label).SequenceEqual(reactions.All().Select(stance => stance.Name)), "resolved reaction HUD names");
Require(rows.Select(row => row.Hint).SequenceEqual(reactions.All().Select(stance => stance.Hint)), "resolved reaction HUD hints");
Require(EcmaJson.Stringify(session.Snapshot()) == before, "reaction projection changed battle state");
var intent = rows.Single(row => row.Key == "reaction:plugin-dodge").Intent!;
Require(intent.Kind == TacticalHudIntentKind.Action && intent.Action is not null, "custom reaction has no canonical action intent");
Require(session.Dispatch(intent.Action!.Value, intent.Stamp.Revision).Accepted, "custom reaction HUD action refused");
Require(session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == id)["reaction"]!.GetValue<string>() == "plugin-dodge", "custom reaction HUD action did not commit");
Require(!session.Dispatch(intent.Action.Value, intent.Stamp.Revision).Accepted && session.GetSnapshot().Revision == 1, "stale reaction HUD intent replayed");
Console.WriteLine("PASS 8 headless resolved-reaction controller/HUD/dispatch assertions; mounted HUD clicks are covered by BattleScreenChecks");
