using Empire.Presentation.Feedback;
using Empire.Checks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Determinism;


if(args.Length!=3) throw new ArgumentException("Usage: CampaignRouteChecks <repository> <source-route.jsonl> <catalog.json>");
var gameText = FixtureText.Load(args[^1]);

var root=args[0];
JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(Path.Combine(root,path)))!;
var packs=Read("godot/data/content-packs.json").AsArray();
var engine=new GrayFlagEngine(packs);
var presentationText = FixtureBattleText.Session(root, engine, gameText);
var campaign=new GrayFlagCampaign(Read("godot/data/grayflag-campaign.json").AsObject(), Read("godot/data/grayflag-campaign-levels.json").AsArray(), engine, presentationText);
var steps=0;var saves=0;var battles=0;
void Equal(string label,JsonNode? actual,JsonNode? expected)
{
 if(JsonNode.DeepEquals(actual,expected)) return;
 throw new Exception($"Route mismatch {label} after {steps} actions\nactual: {actual}\nexpected: {expected}");
}
// Historical source-v1 battle hashes, conclusions, and names require a semantic-schema oracle migration.
foreach(var line in File.ReadLines(args[1]))
{
 var row=JsonNode.Parse(line)!.AsObject();var kind=row["kind"]!.GetValue<string>();
 switch(kind)
 {
 case "header":
  if(row["humanAggression"] is not null) throw new Exception("This runner currently requires the source default AI policy.");
  Equal("initial campaign",campaign.Snapshot(),row["initial"]);break;
 case "advance": campaign.Advance();Equal("advance",campaign.Snapshot(),row["after"]);break;
 case "choose":
  Equal("choices",new JsonArray(campaign.Choices().Select(value=>value!.DeepClone()).ToArray()),row["choices"]);
  campaign.Choose(row["choice"]!.GetValue<string>());Equal("choice",campaign.Snapshot(),row["after"]);break;
 case "begin":
  campaign.BeginBattle();battles++;
  Equal("request",campaign.RequestSnapshot(),row["request"]);Equal("begin campaign",campaign.Snapshot(),row["campaign"]);
  Equal("initial battle",JsonValue.Create(StateDigest.Hash(campaign.Battle!.Snapshot())),row["initialHash"]);break;
 case "step":
  var session=campaign.Battle!;var action=engine.Ai.ChooseAction(session.Snapshot());
  Equal("independently chosen action",action,row["action"]);
  var before=session.Events.Count;
  if(session.IsComputerTurn) { if(!session.AdvanceComputer())throw new Exception("AI did not act"); }
  else { var result=session.Dispatch(JsonSerializer.SerializeToElement(action),session.GetSnapshot().Revision);if(!result.Accepted)throw new Exception(result.Message); }
  Equal("events",new JsonArray(session.Events.Skip(before).Select(value=>(JsonNode)value.DeepClone()).ToArray()),row["events"]);
  Equal("state",JsonValue.Create(StateDigest.Hash(session.Snapshot())),row["hash"]);steps++;break;
 case "checkpoint":
  var own=campaign.Save();
  // Compare source facts, then resume our native document; application envelopes intentionally differ.
  var source=row["document"]!.AsObject();
  Equal("saved campaign",own["campaign"]!["state"],source["campaign"]!["state"]);
  Equal("saved request",campaign.RequestSnapshot(),source["request"]);
  Equal("saved events",own["events"],source["events"]);
  if(own["battle"] is not null) Equal("saved battle",own["battle"]!["state"],source["battle"]!["state"]);
  campaign.Load(own);saves++;break;
 case "complete": campaign.FinishBattle();Equal("result campaign",campaign.Snapshot(),row["after"]);break;
 case "final": Equal("final campaign",campaign.Snapshot(),row["final"]);Console.WriteLine($"PASS native actual campaign route: {battles} battles, {steps} independently selected actions, {saves} source checkpoints/native resumes, terminal {row["status"]}");break;
 default: throw new Exception($"Unexpected source row {kind}");
 }
}
