using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Empire.Localization;
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
var messages = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[^1]))!;
messages["test.reaction.guard.name"] = "替换防御菜单";
messages["test.reaction.guard.hint"] = "来自显式表现元数据";
messages["test.reaction.dodge.name"] = "自定义闪避菜单";
messages["test.reaction.dodge.hint"] = "自定义提示";
var catalog = messages.ToFrozenDictionary(StringComparer.Ordinal);
var gameText = new GameText(key => catalog.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));

var root = args[1];
// Historical source-v1 runtime states still require explicit semantic-schema migration.
using var source = new GZipStream(File.OpenRead(args[0]), CompressionMode.Decompress);
var fixture = JsonNode.Parse(source)!.AsObject();
var reactions = ReactionRegistry.Core();
reactions.Register(new("plugin-dodge", false, 0.4, false, false));
var engine = BattleEngineFactory.Create(new ReadOnlyContent(fixture["packs"]!.AsArray()),
    [new EnginePlugin("test.reaction-adapter", 1, context => context.Replace(BattleCapabilities.Reactions, reactions), overrides: [BattleCapabilities.Reactions])]);
var metadata = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/grayflag-content-text.json")))!.AsObject();
metadata["reactions"]!["guard"] = new JsonObject { ["name"] = "test.reaction.guard.name", ["hint"] = "test.reaction.guard.hint" };
metadata["reactions"]!["plugin-dodge"] = new JsonObject { ["name"] = "test.reaction.dodge.name", ["hint"] = "test.reaction.dodge.hint" };
var presentation = SessionPresentationText.Compose(new(engine, gameText),
    EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/grayflag-campaign-levels.json")))!.AsArray(),
    File.ReadAllText(Path.Combine(root, "godot/data/grayflag-battle-text.json")), EcmaJson.Stringify(metadata));
var state = fixture["turns"]![0]!["before"]!.DeepClone().AsObject();
var session = GrayFlagSession.FromSave(engine, presentation, engine.SaveBattle(state));
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
Require(rows.Select(row => row.Label).SequenceEqual(reactions.Keys().Select(presentation.Content.ReactionName)), "resolved reaction HUD names");
Require(rows.Select(row => row.Hint).SequenceEqual(reactions.Keys().Select(presentation.Content.ReactionHint)), "resolved reaction HUD hints");
Require(EcmaJson.Stringify(session.Snapshot()) == before, "reaction projection changed battle state");
var intent = rows.Single(row => row.Key == "reaction:plugin-dodge").Intent!;
Require(intent.Kind == TacticalHudIntentKind.Action && intent.Action is not null, "custom reaction has no canonical action intent");
Require(session.Dispatch(intent.Action!.Value, intent.Stamp.Revision).Accepted, "custom reaction HUD action refused");
Require(session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == id)["reaction"]!.GetValue<string>() == "plugin-dodge", "custom reaction HUD action did not commit");
Require(!session.Dispatch(intent.Action.Value, intent.Stamp.Revision).Accepted && session.GetSnapshot().Revision == 1, "stale reaction HUD intent replayed");
Console.WriteLine("PASS 8 headless resolved-reaction controller/HUD/dispatch assertions; mounted HUD clicks are covered by BattleScreenChecks");
