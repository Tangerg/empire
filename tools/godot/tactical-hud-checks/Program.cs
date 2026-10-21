using Empire.Presentation.Feedback;
using Empire.Checks;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation;
using Empire.Presentation.Tactical;

if (args.Length != 3) throw new ArgumentException("Usage: TacticalHudChecks <oracle.json> <repository> <catalog.json>");
var gameText = FixtureText.Load(args[^1]);
var root = args[1];
// Historical source-v1 custom levels and runtime states still require explicit semantic-schema migration.
var fixture=EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var engine=new GrayFlagEngine(fixture["packs"]!.AsArray());int checks=0;var failures=new List<string>();
var presentation = FixtureBattleText.Session(root, engine, gameText);
void Require(bool condition,string name){if(!condition)failures.Add(name);checks++;}
string Text(JsonNode? n)=>n is null?"":EcmaJson.ReadString(n);
string Clean(string value)=>Regex.Replace(value,@"\s+"," ").Trim();
foreach(var test in fixture["cases"]!.AsArray().OfType<JsonObject>()){
 var name=Text(test["name"]);var state=test["state"]!.AsObject();var caseEngine=test["packs"] is JsonArray casePacks?new GrayFlagEngine(casePacks):engine;
 var casePresentation = ReferenceEquals(caseEngine, engine) ? presentation : FixtureBattleText.Session(root, caseEngine, gameText);
 var session=GrayFlagSession.FromSave(caseEngine, casePresentation, caseEngine.SaveBattle(state));
 var before=EcmaJson.Stringify(session.Snapshot());var at=new BoardCell(test["tile"]!["x"]!.GetValue<int>(),test["tile"]!["y"]!.GetValue<int>());
 var selection=new TacticalSelection{Kind=Enum.Parse<TacticalSelectionKind>(Text(test["kind"])),UnitId=test["unit"]?.GetValue<int>(),Destination=at,Weapon=test["weapon"] is null?null:Text(test["weapon"]),Ability=test["ability"] is null?null:Text(test["ability"]),Targets=test["target"] is JsonObject target?new[]{new BoardCell(target["x"]!.GetValue<int>(),target["y"]!.GetValue<int>())}:[]};
 var projection=session.ProjectHud(new(19,0),selection,test["cursor"] is JsonObject aimed?new BoardCell(aimed["x"]!.GetValue<int>(),aimed["y"]!.GetValue<int>()):null,test["inspect"]?.GetValue<int>(),test["busy"]?.GetValue<bool>()??false);
 Require(before==EcmaJson.Stringify(session.Snapshot()),name+" pure projection");
 Require(projection.Hint==Text(test["hint"]),name+" authored Game.hudView hint");
 Require(Text(test["sourceComposition"])=="GameController.hudView → Hud.render",name+" actual source composition provenance");
 Require(projection.Sections.Any(section=>section.Key=="tile")==test["hasTile"]!.GetValue<bool>(),name+" exact source tile presence");
 Require(projection.Sections.Any(section=>section.Key=="unit")==test["hasInspection"]!.GetValue<bool>(),name+" exact source inspection presence");
 var swap=projection.Sections.FirstOrDefault(section=>section.Key=="swap");Require((swap is not null)==(test["swap"] is not null),name+" exact source deployment swap presence");if(swap is not null)Require(swap.Rows.Single().Label==$"落在此格：与{Text(test["swap"])}换位",name+" exact source deployment swap name");
 Require(projection.Busy==(test["busy"]?.GetValue<bool>()??false),name+" busy projection");
 if(test["unitName"] is not null)Require(Clean(projection.Sections.Single(section=>section.Key=="unit").Title)==Text(test["unitName"]),name+" lossless source unit heading");
 var rows=projection.Sections.SelectMany(s=>s.Rows).Where(r=>r.Intent is not null).ToArray();var buttons=test["buttons"]!.AsArray().OfType<JsonObject>().ToArray();
 Require(rows.Length==buttons.Length,$"{name} button count {rows.Length} != {buttons.Length}");
 for(int i=0;i<rows.Length;i++){
  var row=rows[i];var button=buttons[i];var intent=row.Intent!;
  Require(row.Enabled==button["enabled"]!.GetValue<bool>(),$"{name}/{i} enabled");
  // Source affordability-only recruitment rows have no disabled HTML attribute but no intent.
  Require(row.Selected==button["selected"]!.GetValue<bool>(),$"{name}/{i} selected");
  var label=Text(button["label"]);
  if(intent.Kind==TacticalHudIntentKind.Back)Require(row.Label.StartsWith("取消")||row.Label=="关闭",name+" back");
  else Require(Clean(row.Label)==Clean(label),$"{name}/{i} label: {row.Label} != {label}");
  if(intent.Kind!=TacticalHudIntentKind.Recruit&& intent.Kind!=TacticalHudIntentKind.Back)
  {
   var expectedHint=Text(button["hint"]);
   if(intent.Action is { } supportAction)
   {
    var order=EcmaJson.Parse(supportAction.GetRawText())!.AsObject();
    var subject=state["units"]!.AsArray().OfType<JsonObject>().Single(unit=>unit["id"]!.GetValue<int>()==order["unit"]!.GetValue<int>());
    IReadOnlyList<Empire.Core.ActionRefusal> reasons=Text(order["kind"]) switch
    {
     "changeFormation"=>caseEngine.Progression.Formations.Options(state,subject).Single(option=>"formation:"+Text(option.Formation["id"])==row.Key).Reasons,
     "changeCareer"=>caseEngine.Progression.Careers.Options(state,subject).Single(option=>Text(option.Career["id"])==Text(order["career"])).Reasons,
     "embark"=>caseEngine.Transport.CarrierOptions(state,subject).Single(option=>option.Carrier["id"]!.GetValue<int>()==order["carrier"]!.GetValue<int>()).Reasons,
     _=>[],
    };
    if(reasons.Count>0) expectedHint=casePresentation.Refusals.DescribeMany(reasons);
   }
   Require(row.Hint==expectedHint,$"{name}/{i} localized owner hint: {row.Hint} != {expectedHint}");
  }
  Require(intent.Stamp==projection.Stamp,name+" stamped intent");
  if(intent.Kind!=TacticalHudIntentKind.Back)
  {
   string argument;
   if(intent.Action is { } action){var payload=EcmaJson.Parse(action.GetRawText())!.AsObject();var kind=Text(payload["kind"]);var key=kind switch {"reaction"=>"stance","face"=>"facing","changeCareer"=>"career","changeFormation"=>"formation","embark"=>"carrier",_=>throw new Exception("Unexpected support action "+kind)};var value=payload[key];argument=value is null?"":value.GetValueKind()==System.Text.Json.JsonValueKind.String?Text(value):value.ToJsonString();}
   else argument=intent.Key??intent.Unit?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";
   Require(argument==Text(button["arg"]),name+" exact source intent argument");
  }
 }
 var forecast=projection.Sections.FirstOrDefault(s=>s.Key=="forecast");
 Require((forecast is not null)==(test["forecast"]!.AsArray().Count>0),name+" exact source forecast eligibility");
 if(test["forecast"]!.AsArray().Count>0){Require(forecast is not null,name+" forecast present");foreach(var line in test["forecast"]!.AsArray()){var text=Clean(Text(line)).Replace(" ","");Require(forecast!.Rows.Any(row=>Clean(row.Label).Replace(" ","").Replace("·","").Replace("反击·","").Replace("→","").Contains(text.Replace("→",""))),name+" forecast line "+text);}}
 string Facts(string value)=>Regex.Replace(value,@"[\s·]","");
 var labels=projection.Sections.SelectMany(section=>section.Rows).Select(row=>Facts(row.Label)).ToArray();
 foreach(var fact in test["accounts"]!.AsArray().Concat(test["terrain"]!.AsArray()).Concat(test["unitText"]!.AsArray()))
  Require(labels.Any(label=>label.Contains(Facts(Text(fact)))),name+" source fact "+Text(fact));
 var objectives=projection.Sections.FirstOrDefault(s=>s.Key=="objectives")?.Rows??[];
 Require(objectives.Count==test["objectives"]!.AsArray().Count,name+" hidden objectives");
 for(int i=0;i<objectives.Count;i++)Require(Clean(objectives[i].Label.Replace(" · ",""))==Clean(Text(test["objectives"]![i])),name+" objective labels");
 try{session.ProjectHud(new(19,1),selection,at,null,false);throw new Exception("stale HUD accepted");}catch(Empire.Core.DomainInvariantException){checks++;}
}
var fogState=fixture["cases"]![1]!["state"]!.DeepClone().AsObject();fogState["rules"]!["fog"]=true;
var visible=engine.Space.VisibleUnits(fogState,1).Select(unit=>unit["id"]!.GetValue<int>()).ToHashSet();
var hidden=fogState["units"]!.AsArray().OfType<JsonObject>().FirstOrDefault(unit=>!visible.Contains(unit["id"]!.GetValue<int>()));
Require(hidden is not null,"Hidden forged selection witness");
if(hidden is not null){var session=GrayFlagSession.FromSave(engine, presentation, engine.SaveBattle(fogState));var id=hidden["id"]!.GetValue<int>();var forged=session.ProjectHud(new(5,0),new(){Kind=TacticalSelectionKind.Destination,UnitId=id,Destination=new(hidden["x"]!.GetValue<int>(),hidden["y"]!.GetValue<int>())},null,id,false);Require(!forged.Sections.Any(section=>section.Key is "unit" or "forecast" or "commands"),"Hidden forged selected/inspect IDs expose no private unit/menu/forecast");}
if(failures.Count>0)throw new Exception(string.Join("\n",failures));
Console.WriteLine($"HUD actual-source DOM differential: {checks} assertions passed.");
