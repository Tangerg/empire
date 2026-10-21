using System.Text.Json.Nodes;
using Empire.Core.Campaign;
using Empire.Core.Determinism;
var root=Path.GetFullPath(args.Length>0?args[0]:"godot");
var diagnostics=args.Length>3?args[3]:Path.GetTempPath();
var fixture=JsonNode.Parse(File.ReadAllText(args.Length>1?args[1]:"/tmp/empire-campaign-stage/oracle.json"))!.AsObject();
var definition=fixture["definition"]!.AsObject();
var packs=JsonNode.Parse(File.ReadAllText(root+"/data/content-packs.json"))!.AsArray();
var levels=JsonNode.Parse(File.ReadAllText(root+"/data/grayflag-campaign-levels.json"))!.AsArray();
var bridge=new CampaignBattleBridge(id=>levels.Select(l=>l!.AsObject()).First(l=>l["id"]!.GetValue<string>()==id),new Empire.Core.State.ReadOnlyContent(packs));
void Equal(JsonNode? actual,JsonNode? expected,string name)
{
 if(actual is null || expected is null ? actual is not null || expected is not null : StateDigest.Hash(actual)!=StateDigest.Hash(expected))
 {File.WriteAllText(Path.Combine(diagnostics,"actual.json"),actual?.ToJsonString()??"null");File.WriteAllText(Path.Combine(diagnostics,"expected.json"),expected?.ToJsonString()??"null");throw new Exception(name);}
}
foreach(var entry in fixture["operations"]!.AsArray())
{
 var item=entry!.AsObject();var runtime=new CampaignRuntime(definition,item["before"]!.AsObject());
 var arguments=item["args"]!;JsonNode? value=null;string? error=null;
 try{value=item["operation"]!.GetValue<string>() switch {
 "advance"=>runtime.Advance(),"choose"=>runtime.Choose(arguments["id"]!.GetValue<string>()),"choices"=>runtime.Choices(),
 "begin"=>runtime.BeginBattle(bridge),"complete"=>runtime.CompleteBattle(arguments["result"]!.AsObject()),_=>throw new Exception("unknown operation")};}
 catch(Exception e){error=e.Message;}
 if(error!=item["error"]?.GetValue<string>())throw new Exception($"{item["name"]}: error {error} / {item["error"]}");
 if(error is null)Equal(value,item["value"],$"return mismatch {item["name"]}");
 Equal(runtime.Snapshot(),item["after"],$"state mismatch {item["name"]}");
}
Console.WriteLine($"PASS {fixture["operations"]!.AsArray().Count} source campaign runtime operation/rollback cases");
foreach(var entry in fixture["policies"]!.AsArray())
{
 var item=entry!.AsObject();var state=item["state"]!.AsObject();
 var value=item["kind"]!.GetValue<string>()=="context"?Candidate01CampaignPolicy.ApplyBattleContext(item["request"]!.AsObject(),state):
 Candidate01CampaignPolicy.ApplyBattleResult(item["result"]!.AsObject(),state);
 Equal(value,item["expected"],"candidate policy mismatch");
}
Console.WriteLine($"PASS {fixture["policies"]!.AsArray().Count} source authored campaign policy cases");
foreach(var entry in fixture["results"]!.AsArray())
{
 var item=entry!.AsObject();JsonObject value;
 if(item["prepare"]?.GetValue<bool>()==true)value=bridge.Prepare(definition,item["state"]!.AsObject());
 else value=bridge.Result(item["request"]!.AsObject(),item["state"]!.AsObject(),item["events"]!.AsArray().Select(e=>e!.AsObject()),item["outcome"]?.GetValue<string>());
 Equal(value,item["expected"],"bridge mismatch");
}
Console.WriteLine($"PASS {fixture["results"]!.AsArray().Count} source roster bridge projection cases");

if(args.Length>2)CampaignValidationChecks.Run(args[2]);
if(args.Length>4)CampaignRegistryChecks.Run(args[4],Equal);
