using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Determinism;

if(args.Length!=2) throw new ArgumentException("Usage: CampaignRouteChecks <repository> <source-route.jsonl>");
var root=args[0];
JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(Path.Combine(root,path)))!;
var packs=Read("godot/data/content-packs.json").AsArray();
var engine=new GrayFlagEngine(packs);
var campaign=new GrayFlagCampaign(Read("godot/data/grayflag-campaign.json").AsObject(),Read("godot/data/grayflag-campaign-levels.json").AsArray(),packs,engine);
var steps=0;var saves=0;var battles=0;
void Equal(string label,JsonNode? actual,JsonNode? expected)
{
 if(JsonNode.DeepEquals(actual,expected)) return;
 throw new Exception($"Route mismatch {label} after {steps} actions\nactual: {actual}\nexpected: {expected}");
}
foreach(var line in File.ReadLines(args[1]))
{
 var row=JsonNode.Parse(line)!.AsObject();var kind=row["kind"]!.GetValue<string>();
 switch(kind)
 {
 case "header":
  if(row["humanAggression"] is not null) throw new Exception("This runner currently requires the source default AI policy.");
  Equal("initial campaign",campaign.Runtime.Snapshot(),row["initial"]);break;
 case "advance": campaign.Runtime.Advance();Equal("advance",campaign.Runtime.Snapshot(),row["after"]);break;
 case "choose":
  Equal("choices",new JsonArray(campaign.Runtime.Choices().Select(value=>value!.DeepClone()).ToArray()),row["choices"]);
  campaign.Runtime.Choose(row["choice"]!.GetValue<string>());Equal("choice",campaign.Runtime.Snapshot(),row["after"]);break;
 case "begin":
  campaign.BeginBattle();battles++;
  Equal("request",campaign.Request,row["request"]);Equal("begin campaign",campaign.Runtime.Snapshot(),row["campaign"]);
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
  // Timestamps are writer metadata; compare canonical states, request and event history.
  var source=row["document"]!.AsObject();
  Equal("saved campaign",own["campaign"]!["state"],source["campaign"]!["state"]);
  Equal("saved request",own["request"],source["request"]);
  Equal("saved events",own["events"],source["events"]);
  if(own["battle"] is not null) Equal("saved battle",own["battle"]!["state"],source["battle"]!["state"]);
  campaign.Load(source);saves++;break;
 case "complete": campaign.FinishBattle();Equal("result campaign",campaign.Runtime.Snapshot(),row["after"]);break;
 case "final": Equal("final campaign",campaign.Runtime.Snapshot(),row["final"]);Console.WriteLine($"PASS native actual campaign route: {battles} battles, {steps} independently selected actions, {saves} source save/resumes, terminal {row["status"]}");break;
 default: throw new Exception($"Unexpected source row {kind}");
 }
}
