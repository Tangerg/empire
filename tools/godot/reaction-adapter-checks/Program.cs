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

using var source = new GZipStream(File.OpenRead(args[0]), CompressionMode.Decompress);
var fixture = JsonNode.Parse(source)!.AsObject();
var reactions = ReactionRegistry.Core();
reactions.Replace(reactions.Get("guard") with { Name = "替换防御菜单", Hint = "来自已解析注册表" });
reactions.Register(new("plugin-dodge", "自定义闪避菜单", false, 0.4, false, false, "自定义提示"));
var engine = BattleEngineFactory.Create(new ReadOnlyContent(fixture["packs"]!.AsArray()),
    [new EnginePlugin("test.reaction-adapter", 1, context => context.Replace(BattleCapabilities.Reactions, reactions), overrides: [BattleCapabilities.Reactions])]);
var state = fixture["turns"]![0]!["before"]!.DeepClone().AsObject();
var session = GrayFlagSession.FromSave(engine, engine.SaveBattle(state));
var actor = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().First(unit => engine.Turns.MayAct(state, unit));
var id = actor["id"]!.GetValue<int>();
var before = EcmaJson.Stringify(session.Snapshot());
var choices = session.GetLegalOrders(id).Where(choice => choice.Action.GetProperty("kind").GetString() == "reaction").ToArray();
void Require(bool result, string message) { if (!result) throw new Exception(message); }
Require(choices.Select(choice => choice.Action.GetProperty("stance").GetString()).SequenceEqual(reactions.Keys()), "resolved reaction order menu IDs");
Require(choices.Select(choice => choice.Label).SequenceEqual(reactions.All().Select(stance => $"反应：{stance.Name}")), "resolved reaction order menu names");
var selection = new TacticalSelection { Kind = TacticalSelectionKind.Destination, UnitId = id, Destination = new BoardCell(actor["x"]!.GetValue<int>(), actor["y"]!.GetValue<int>()) };
var hud = session.ProjectHud(new(7,0), selection, null, id, false);
var rows = hud.Sections.Single(section => section.Key == "stances").Rows.Where(row => row.Key.StartsWith("reaction:", StringComparison.Ordinal)).ToArray();
Require(rows.Select(row => row.Key).SequenceEqual(reactions.Keys().Select(stance => "reaction:" + stance)), "resolved reaction HUD IDs");
Require(rows.Select(row => row.Label).SequenceEqual(reactions.All().Select(stance => stance.Name)), "resolved reaction HUD names");
Require(rows.Select(row => row.Hint).SequenceEqual(reactions.All().Select(stance => stance.Hint)), "resolved reaction HUD hints");
Require(EcmaJson.Stringify(session.Snapshot()) == before, "reaction menus changed battle state");
Console.WriteLine("PASS 6 headless resolved-reaction application menu/HUD assertions; this is not GUI acceptance");
