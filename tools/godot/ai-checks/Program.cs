using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Text.Json;
using Empire.Core.Tactics;
using Empire.Core.Ai;
using Empire.Core.Engine;
using Empire.Core.Determinism;
var root=args.Length>0?Path.GetFullPath(args[0]):Path.GetFullPath("godot");
var engine=new GrayFlagEngine(JsonNode.Parse(File.ReadAllText(root+"/data/content-packs.json"))!.AsArray());
var planner=engine.Ai;
var diagnostics=args.Length>2?args[2]:Path.GetTempPath();
JsonNode Read(string path) { if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!; using var file=File.OpenRead(path+".gz");using var gzip=new GZipStream(file,CompressionMode.Decompress);return JsonNode.Parse(gzip)!; }
var count=0;
foreach(var item in Read(root+"/tests/oracle/action-cases.json").AsArray())
{
 var state=item!["before"]!.AsObject();var chosen=planner.ChooseAction(state); var expected=item["action"]!;
 if(StateDigest.Hash(chosen)!=StateDigest.Hash(expected))
 {File.WriteAllText(Path.Combine(diagnostics,"actual.json"),chosen.ToJsonString());File.WriteAllText(Path.Combine(diagnostics,"state.json"),state.ToJsonString());throw new Exception($"Choice mismatch {count}: actual {chosen} expected {expected}");}
 count++;
}
Console.WriteLine($"PASS {count} exact source policy choices");
foreach(var aggression in new[]{"0.35","0.58","0.8"})
{
 var fixture=JsonNode.Parse(File.ReadAllText(root+$"/tests/oracle/grayflag-{aggression}.json"))!.AsObject();
 var battle=engine.CreateBattle(fixture["level"]!.AsObject());var step=0;
 foreach(var expected in fixture["steps"]!.AsArray())
 {
  var action=planner.ChooseAction(battle.Snapshot());
  if(StateDigest.Hash(action)!=StateDigest.Hash(expected!["action"]!))throw new Exception($"Autonomous choice mismatch {aggression}:{step}: {action} vs {expected["action"]}");
  var receipt=battle.Dispatch(action);var events=new JsonArray(receipt.Events.Select(e=>(JsonNode)e.DeepClone()).ToArray());
  if(StateDigest.Hash(events)!=StateDigest.Hash(expected["events"]!))throw new Exception($"Autonomous event mismatch {aggression}:{step}");
  if(receipt.StateHash!=expected["stateHash"]!.GetValue<string>())throw new Exception($"Autonomous state mismatch {aggression}:{step}");
  step++;
 }
 Console.WriteLine($"PASS autonomous source policy/events/state stream aggression {aggression}, {step} actions, phase {battle.Snapshot()["phase"]}");
}

if(args.Length>1)
{
 var focused=Read(args[1]).AsArray();
 foreach(var item in focused)
 {
  var state=item!["state"]!.AsObject();
  var context=new AiTurnContext(planner.Planning,state,state["currentPlayer"]!.GetValue<int>(),item["aggression"]!.GetValue<double>());
  var agenda=context.Agenda;
  var projection=new {
   captureTargets=agenda.CaptureTargets.Select(t=>new{at=TacticalQueries.CoordinateJson(t.At),weight=t.Weight}),
   enemyHqs=agenda.EnemyHqs.Select(TacticalQueries.CoordinateJson),myHqs=agenda.MyHqs.Select(TacticalQueries.CoordinateJson),
   mission=new {
    destinations=agenda.Mission.Destinations.Select(d=>new{at=TacticalQueries.CoordinateJson(d.At),weight=d.Weight,unitIds=d.UnitIds,captureOnly=d.CaptureOnly,reason=d.Reason}),
    priorityUnits=agenda.Mission.PriorityUnits.Select(pair=>new object[]{pair.Key,pair.Value}),
    priorityStructures=agenda.Mission.PriorityStructures.Select(pair=>new object[]{pair.Key,pair.Value}),
    protectedUnits=agenda.Mission.ProtectedUnits.Select(pair=>new object[]{pair.Key,pair.Value})
   }
  };
  var actual=JsonSerializer.SerializeToNode(projection)!;
  if(StateDigest.Hash(actual)!=StateDigest.Hash(item["agenda"]!))
  {File.WriteAllText(Path.Combine(diagnostics,"agenda-actual.json"),actual.ToJsonString());File.WriteAllText(Path.Combine(diagnostics,"agenda-expected.json"),item["agenda"]!.ToJsonString());throw new Exception($"Agenda mismatch {item["name"]}");}
  var threat=JsonSerializer.SerializeToNode(context.Threat.Select(pair=>new object[]{pair.Key,pair.Value}))!;
  if(StateDigest.Hash(threat)!=StateDigest.Hash(item["threat"]!))throw new Exception($"Threat mismatch {item["name"]}");
  foreach(var appraisal in item["appraisals"]!.AsArray())
  {
   var unit=TacticalQueries.RequireUnit(state,appraisal!["unit"]!.GetValue<int>());
   var value=new TileAppraisal(context,unit,TacticalQueries.Position(appraisal["at"]!.AsObject())).Value;
   if(value!=appraisal["value"]!.GetValue<double>())throw new Exception($"Appraisal mismatch {item["name"]}: {value:R} vs {appraisal["value"]}");
  }
  if(item["action"] is {} action && StateDigest.Hash(planner.ChooseAction(state,context.Aggression))!=StateDigest.Hash(action))throw new Exception($"Focused policy mismatch {item["name"]}");
 }
 Console.WriteLine($"PASS {focused.Count} exact source focused agenda/threat/appraisal/policy cases");
}
var intents=planner.Intents.Clone();
intents.Register(new AiIntent("early",-100,_=>new JsonObject{["kind"]="test"}));
if(intents.Ids()[0]!="early"||planner.Intents.Keys().Contains("early"))throw new Exception("Registry clone is not independent");
intents.Replace(new AiIntent("early",-100,_=>null));
intents.Seal();
try{intents.Register(new AiIntent("late",0,_=>null));throw new Exception("Sealed registry accepted registration");}
catch(Empire.Core.DomainInvariantException){}
Console.WriteLine("PASS AI registry extension, replacement, clone isolation and sealing");
