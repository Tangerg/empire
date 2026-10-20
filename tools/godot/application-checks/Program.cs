using System.Text.Json.Nodes;
using System.Text.Json;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Determinism;
var root = args.Length > 0 ? args[0] : ".";
JsonNode Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(root,"godot/data",file)))!;
var packs=Read("content-packs.json").AsArray(); var engine=new GrayFlagEngine(packs);
GrayFlagCampaign Create()=>new(Read("grayflag-campaign.json").AsObject(),Read("grayflag-campaign-levels.json").AsArray(),packs,engine);
var campaign=Create();
while(campaign.Runtime.Node()["type"]!.GetValue<string>()!="battle")
{ if(campaign.Runtime.Node()["type"]!.GetValue<string>()=="choice")campaign.Runtime.Choose(campaign.Runtime.Choices()[0]!["id"]!.GetValue<string>());else campaign.Runtime.Advance(); }
campaign.BeginBattle();
var initial=StateDigest.Hash(campaign.Battle!.Snapshot());
Act(campaign);campaign.RestartBattle();
if(StateDigest.Hash(campaign.Battle.Snapshot())!=initial)throw new Exception("Restart did not restore initial battle");
for(var step=0;step<15;step++) Act(campaign);
var saved=campaign.Save();var restored=Create();restored.Load(saved);
if(StateDigest.Hash(restored.Battle!.Snapshot())!=StateDigest.Hash(campaign.Battle!.Snapshot()))throw new Exception("Battle save roundtrip mismatch");
var actions=0;
while(campaign.Battle.Snapshot()["phase"]!.GetValue<string>()!="over")
{
 if(++actions>5000)throw new Exception("Battle did not terminate within diagnostic guard; not a pass");
 Act(campaign);Act(restored);
 if(StateDigest.Hash(restored.Battle!.Snapshot())!=StateDigest.Hash(campaign.Battle.Snapshot()))throw new Exception("Restored action divergence");
}
campaign.FinishBattle();restored.FinishBattle();
if(StateDigest.Hash(campaign.Runtime.Snapshot())!=StateDigest.Hash(restored.Runtime.Snapshot()))throw new Exception("Campaign result divergence after save");
var before=StateDigest.Hash(restored.Runtime.Snapshot());var invalid=saved.DeepClone().AsObject();invalid["request"]!["id"]="wrong";
try {restored.Load(invalid);throw new Exception("Invalid save accepted");}catch(InvalidOperationException){}
if(StateDigest.Hash(restored.Runtime.Snapshot())!=before)throw new Exception("Invalid load mutated campaign");
Console.WriteLine($"PASS application story/choice -> battle -> midbattle save/load -> {actions} matching actions -> campaign result; invalid load atomic");
void Act(GrayFlagCampaign value)
{
 var battle=value.Battle!;
 if(battle.IsComputerTurn){battle.AdvanceComputer();return;}
 var result=battle.Dispatch(JsonSerializer.SerializeToElement(engine.Ai.ChooseAction(battle.Snapshot())),battle.GetSnapshot().Revision);
 if(!result.Accepted)throw new Exception(result.Message);
}
