using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Transactions;
var root=Path.GetFullPath(args.Length>0?args[0]:"godot");
if(args.Length<2)throw new ArgumentException("Pass a source JSONL fixture");
var fixture=Path.GetFullPath(args[1]);var diagnostics=args.Length>2?args[2]:Path.GetDirectoryName(fixture)!;
Directory.CreateDirectory(diagnostics);
var engine=new GrayFlagEngine(JsonNode.Parse(File.ReadAllText(root+"/data/content-packs.json"))!.AsArray());
using var reader=File.OpenText(fixture);
var header=JsonNode.Parse(reader.ReadLine()!)!.AsObject();
var level=header["level"]!.AsObject();var id=level["id"]!.GetValue<string>();
Battle? battle=null;var index=0;string phase="initial";JsonObject? action=null;
void Equal(JsonNode actual,JsonNode expected,string subject){if(StateDigest.Hash(actual)!=StateDigest.Hash(expected))throw new Exception(subject+" mismatch");}
try {
 battle=engine.CreateBattle(level);
 if(battle.StateHash!=header["initialHash"]!.GetValue<string>())throw new Exception("initial hash mismatch");
 string? line;bool ended=false;
 while((line=reader.ReadLine()) is not null) {
  var row=JsonNode.Parse(line)!.AsObject();var kind=row["kind"]!.GetValue<string>();
  if(kind=="final") {if(battle.Snapshot()["phase"]!.GetValue<string>()!="over")throw new Exception("expected terminal battle");ended=true;break;}
  if(kind=="error")throw new Exception("source oracle failed: "+row["message"]);
  phase="choose";var before=battle.Snapshot();action=engine.Ai.ChooseAction(before);
  Equal(action,row["action"]!,"chosen action");
  phase="dispatch";var receipt=battle.Dispatch(action);
  phase="events";Equal(new JsonArray(receipt.Events.Select(value=>(JsonNode)value.DeepClone()).ToArray()),row["events"]!,"event sequence");
  phase="state";if(receipt.StateHash!=row["stateHash"]!.GetValue<string>())throw new Exception("state hash mismatch");
  index++;
 }
 if(!ended)throw new Exception("source fixture incomplete, terminal result not observed");
 Console.WriteLine($"PASS native {id}: {index} independently chosen actions and exact events/state, winner {battle.Snapshot()["winnerTeam"]}");
 File.WriteAllText(Path.Combine(diagnostics,id+".native-pass"),$"{index}\n");
} catch(Exception error) {
 var failure=new JsonObject{["level"]=id,["index"]=index,["phase"]=phase,["message"]=error.Message,["actualAction"]=action?.DeepClone(),["stateHash"]=battle?.StateHash};
 File.WriteAllText(Path.Combine(diagnostics,id+".native-failure.json"),failure.ToJsonString());
 File.WriteAllText(Path.Combine(diagnostics,id+".native-state.json"),(battle?.Snapshot()??level).ToJsonString());
 Console.Error.WriteLine($"FAIL native {id} action {index} during {phase}: {error.Message}");
 Environment.ExitCode=1;
}
