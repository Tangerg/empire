using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Determinism;
var engine=new GrayFlagEngine(JsonNode.Parse(File.ReadAllText("godot/data/content-packs.json"))!.AsArray());var count=0;
foreach(var item in JsonNode.Parse(File.ReadAllText(args[0]))!.AsArray())
{
 var before=item!["before"]!.AsObject();var battle=engine.LoadSavedBattle(engine.SaveBattle(before));var events=new JsonArray();string? error=null;
 try {var receipt=battle.Dispatch(item["action"]!.AsObject());foreach(var value in receipt.Events)events.Add(value.DeepClone());}catch(Empire.Core.IllegalActionException e){error=e.Message;}
 if(error!=item["error"]?.GetValue<string>())throw new Exception($"{item["name"]}: error {error} vs {item["error"]}");
 if(StateDigest.Hash(events)!=StateDigest.Hash(item["events"]!))throw new Exception($"{item["name"]}: events differ");
 if(StateDigest.Hash(battle.Snapshot())!=StateDigest.Hash(item["after"]!))throw new Exception($"{item["name"]}: state differs");
 count++;
}
Console.WriteLine($"PASS {count} source recruitment action/event/state cases");
