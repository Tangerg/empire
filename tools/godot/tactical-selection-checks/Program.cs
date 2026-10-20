using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Determinism;
using Empire.Presentation.Tactical;

var fixture = new Fixture(JsonDocument.Parse(File.ReadAllText(args[0])).RootElement.Clone());
var controller = new TacticalSelectionController(fixture);
int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new InvalidOperationException(name); }
void Equal<T>(T actual, T expected, string name) => Check(JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(expected), name + $" actual={JsonSerializer.Serialize(actual)} expected={JsonSerializer.Serialize(expected)}");
BoardCell? CellOrNull(JsonElement element) => element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : Fixture.Cell(element);
foreach (var entry in fixture.Root.GetProperty("trace").EnumerateArray())
{
    var op = entry.GetProperty("op");
    var name = op[0].GetString();
    var result = name switch
    {
        "back" => controller.Back(controller.Stamp),
        "hover" => controller.Hover(controller.Stamp, new BoardCell(op[1].GetInt32(), op[2].GetInt32())),
        _ => controller.Click(controller.Stamp, new BoardCell(op[1].GetInt32(), op[2].GetInt32())),
    };
    var expectedAction = entry.GetProperty("action");
    Check(JsonNode.DeepEquals(result.Request?.Action is { } action ? JsonNode.Parse(action.GetRawText()) : null,
        expectedAction.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(expectedAction.GetRawText())), $"source action {op}");
    if (result.Request is not null) controller.CompleteDispatch(controller.Stamp, fixture);
    var expected = entry.GetProperty("view");
    Equal(controller.Selection.Kind.ToString(), expected.GetProperty("kind").GetString(), $"source selection {op}");
    Equal(controller.Selection.UnitId, expected.GetProperty("unitId").ValueKind == JsonValueKind.Null ? (int?)null : expected.GetProperty("unitId").GetInt32(), "source unit");
    Equal(controller.Selection.Destination, CellOrNull(expected.GetProperty("destination")), "source destination");
    Equal(controller.Selection.Ability, expected.GetProperty("ability").GetString(), "source ability");
    Equal(controller.Selection.Weapon, expected.GetProperty("weapon").GetString(), "source weapon");
    Equal(controller.Selection.Targets, Fixture.Cells(expected.GetProperty("targets")), "source targets");
    var overlay = controller.Overlay();
    Equal(overlay.Selected, CellOrNull(expected.GetProperty("selected")), "source selected overlay");
    Equal(overlay.Path, Fixture.Cells(expected.GetProperty("path")), "source path");
    Equal(overlay.Move, Fixture.Cells(expected.GetProperty("move")), "source move");
    Equal(overlay.Attack, Fixture.Cells(expected.GetProperty("attack")), "source attack");
    Equal(overlay.Helpful, Fixture.Cells(expected.GetProperty("helpful")), "source helpful");
    Equal(overlay.Controlled, Fixture.Cells(expected.GetProperty("controlled")), "source control");
    Equal(overlay.Threat, Fixture.Cells(expected.GetProperty("threat")), "source inspected threat");
}
foreach (var entry in fixture.Root.GetProperty("overlays").EnumerateArray())
{
    if(entry.GetProperty("hidden").GetBoolean())fixture.UnitsOverride=new[]{new TacticalUnit(1,new(0,0),true,true),new TacticalUnit(2,new(19,0),false,false)};
    controller.Bind(fixture);
    if(entry.GetProperty("picked").GetBoolean())controller.Click(controller.Stamp,new(0,0));
    controller.SetBusy(entry.GetProperty("busy").GetBoolean());
    controller.Hover(controller.Stamp,Fixture.Cell(entry.GetProperty("hover")));
    var expected=entry.GetProperty("view");var overlay=controller.Overlay();var name=entry.GetProperty("name").GetString();
    Equal(overlay.Selected,CellOrNull(expected.GetProperty("selected")),"full source overlay selected "+name);
    Equal(overlay.Cursor,CellOrNull(expected.GetProperty("cursor")),"full source overlay cursor "+name);
    Equal(overlay.Move,Fixture.Cells(expected.GetProperty("move")),"full source overlay move "+name);
    Equal(overlay.Attack,Fixture.Cells(expected.GetProperty("attack")),"full source overlay attack "+name);
    Equal(overlay.Helpful,Fixture.Cells(expected.GetProperty("helpful")),"full source overlay helpful "+name);
    Equal(overlay.Controlled,Fixture.Cells(expected.GetProperty("controlled")),"full source overlay control "+name);
    Equal(overlay.Path,Fixture.Cells(expected.GetProperty("path")),"full source overlay path "+name);
    Equal(overlay.Threat,Fixture.Cells(expected.GetProperty("threat")),"full source overlay inspected threat "+name);
    fixture.UnitsOverride=null;
}
controller.Bind(fixture);
foreach (var entry in fixture.Root.GetProperty("keyboard").EnumerateArray())
{
    var op = entry.GetProperty("op");
    var result = op[0].GetString() == "key" ? controller.Key(controller.Stamp,op[1].GetString()!)
        : controller.Click(controller.Stamp,new(op[1].GetInt32(),op[2].GetInt32()));
    var expectedAction = entry.GetProperty("action");
    Check(JsonNode.DeepEquals(result.Request?.Action is { } action ? JsonNode.Parse(action.GetRawText()) : null,
        expectedAction.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(expectedAction.GetRawText())), $"source controller action {op}");
    if (result.Request is not null) controller.CompleteDispatch(controller.Stamp,fixture);
    Equal(controller.Selection.Kind.ToString(),entry.GetProperty("kind").GetString(),$"source keyboard selection {op}");
    Equal(controller.Cursor,CellOrNull(entry.GetProperty("cursor")), $"source keyboard cursor {op}");
    Equal(result.PreventDefault,entry.GetProperty("prevented").GetBoolean(),$"source preventDefault {op}");
}
controller.Bind(fixture);
var hoverLeave=fixture.Root.GetProperty("hoverLeave");
controller.SetBusy(true);controller.Hover(controller.Stamp,new(3,0));
Equal(controller.Cursor,CellOrNull(hoverLeave.GetProperty("hover").GetProperty("cursor")),"source busy hover cursor");
Equal(controller.InspectId,hoverLeave.GetProperty("hover").GetProperty("inspect").GetInt32(),"source busy hover inspection");
controller.Leave(controller.Stamp);
Equal(controller.Cursor,CellOrNull(hoverLeave.GetProperty("leave").GetProperty("cursor")),"source leave clears cursor");
Equal(controller.InspectId,hoverLeave.GetProperty("leave").GetProperty("inspect").GetInt32(),"source busy leave preserves inspection");
Check(controller.Leave(controller.Stamp with {Binding=99}).Status==TacticalInputStatus.StaleBinding,"stale leave binding");
controller.Bind(fixture);
var stamp = controller.Stamp;
Check(controller.Click(stamp with { Binding = 99 }, new(0,0)).Status == TacticalInputStatus.StaleBinding, "stale binding");
Check(controller.Click(stamp with { Revision = 99 }, new(0,0)).Status == TacticalInputStatus.StaleRevision, "stale revision");
controller.SetBusy(true);
Check(controller.Click(stamp,new(0,0)).Status == TacticalInputStatus.Busy, "busy click");
Check(controller.Key(stamp,"Escape").Status == TacticalInputStatus.Busy, "busy escape");
Check(controller.Overlay().Move.Count == 0, "busy overlays");
controller.SetBusy(false);
fixture.HumanTurn = false; fixture.HumanInput = false;
Check(controller.Key(stamp,"e").Status == TacticalInputStatus.NotHuman, "AI end turn");
Check(controller.Back(stamp).Status == TacticalInputStatus.Ready, "AI escape precedence");
fixture.HumanTurn = true; fixture.HumanInput = true;
fixture.Phase = "over";
Check(controller.Click(stamp,new(0,0)).Status == TacticalInputStatus.Terminal, "terminal click");
fixture.Phase = "playing";
controller.Bind(fixture);
controller.Click(stamp,new(0,0)); controller.Click(stamp,new(2,0));
var wait = controller.Key(stamp,"w");
Check(wait.Request?.Action?.GetProperty("command").GetProperty("ability").GetString() == "wait", "command before WASD");
Check(controller.Cursor is null, "command does not move cursor");
controller.CompleteDispatch(stamp,fixture);
controller.Key(stamp," ");
Equal(controller.Selection.UnitId,fixture.Actors()[0],"cycle source actor order");
controller.Hover(stamp,new(0,0));
fixture.StepsOverride = new[] { new BoardCell(1,1), new BoardCell(1,0) };
controller.Key(stamp,"d");
Equal(controller.Cursor,new BoardCell(1,1),"configured direction strict tie");
fixture.StepsOverride = null;
controller.Bind(fixture);
controller.Click(stamp,new(0,0)); controller.Click(stamp,new(2,0));
var single = controller.ChooseCommand(stamp,"attack");
Check(single.Request?.Action?.GetProperty("command").TryGetProperty("target",out _) == true,"single target auto dispatch");
controller.CompleteDispatch(stamp,fixture);
controller.Click(stamp,new(0,0)); controller.Click(stamp,new(2,0));
fixture.CommandsOverride = new[] { new TacticalCommand("blessing", "customHelp", null, false, false, new[] {new BoardCell(0,0),new BoardCell(1,0)}) };
controller.ChooseCommand(stamp,"blessing");
Equal(controller.Selection.Kind,TacticalSelectionKind.Target,"multiple targets arm");
Equal(controller.Overlay().Helpful.Count,2,"engagement metadata helpful");
Equal(controller.Overlay().Attack.Count,0,"engagement not ability ID");
controller.Back(stamp); Equal(controller.Selection.Kind,TacticalSelectionKind.Destination,"target back");
controller.Back(stamp); Equal(controller.Selection.Kind,TacticalSelectionKind.Unit,"destination back");
controller.Back(stamp); Equal(controller.Selection.Kind,TacticalSelectionKind.Idle,"unit back");
fixture.CommandsOverride = null;
fixture.TacticsOverride = new[] { new TacticalTactic("c","t",new[]{new BoardCell(0,0),new BoardCell(1,0)}) };
controller.ChooseTactic(stamp,"c:t"); Equal(controller.Selection.Kind,TacticalSelectionKind.Tactic,"tactic arm");
Check(controller.Click(stamp,new(1,0)).Request?.Action?.GetProperty("kind").GetString()=="tactic","tactic dispatch");
controller.CompleteDispatch(stamp,fixture);
controller.Click(stamp,new(0,0));
fixture.PassengersOverride = new[] { new TacticalPassenger(9,new[]{new BoardCell(1,0)}) };
controller.ChooseDisembark(stamp,9); Equal(controller.Selection.Kind,TacticalSelectionKind.Disembark,"disembark arm");
Equal(controller.Overlay().Helpful.Count,1,"disembark helpful");
controller.Back(stamp); Equal(controller.Selection.Kind,TacticalSelectionKind.Idle,"disembark source default back");
fixture.Phase = "deployment"; fixture.HumanTurn = false;
controller.Bind(fixture);
controller.Click(stamp,new(0,0)); Equal(controller.Selection.Kind,TacticalSelectionKind.DeploymentUnit,"deployment pick");
Check(controller.Click(stamp,new(1,0)).Request?.Action?.GetProperty("kind").GetString()=="deployUnit","deployment dispatch");
controller.CompleteDispatch(stamp,fixture); Equal(controller.Selection.Kind,TacticalSelectionKind.Deploying,"deployment settles");
Check(controller.Key(stamp,"arrowright").Status==TacticalInputStatus.NotHuman,"source keyboard deployment gate");
fixture.Phase="playing"; fixture.HumanTurn=true;
controller.Bind(fixture);controller.Click(stamp,new(0,0));
// A legal refusal resets composition just as a successful settled request does.
controller.SetBusy(true);controller.CompleteDispatch(stamp,fixture);
Equal(controller.Selection.Kind,TacticalSelectionKind.Idle,"failed action reset");
// Equal scores must retain first stop and first option, including weapon key order.
fixture.StopsOverride = new[] { new BoardCell(2,0), new BoardCell(2,1) };
fixture.CommandsOverride = new[] {
    new TacticalCommand("z", "volley", "zWeapon", false, true, new[] { new BoardCell(3,0) }),
    new TacticalCommand("a", "volley", "aWeapon", false, true, new[] { new BoardCell(3,0) }) };
fixture.AttackOverride = new TacticalAttack(10,0,0,0,true,new[] {new BoardCell(3,0)});
controller.Click(stamp,new(0,0));controller.Click(stamp,new(3,0));
Equal(controller.Selection.Destination,new BoardCell(2,0),"best spot strict tie preserves stops");
Equal(controller.Selection.Weapon,"zWeapon","best spot preserves option order");
Equal(controller.Selection.Ability,"volley","best spot preserves custom ability");
fixture.StopsOverride=null;fixture.CommandsOverride=null;fixture.AttackOverride=null;
// Compare escaped UTF-16 code units via the canonical boundary, not GetString/DeepEquals.
var utf16 = EcmaJson.Parse(fixture.Root.GetProperty("utf16").GetRawText())!.AsObject();
string Strange(string key) => EcmaJson.ReadString(utf16["strange"]![key]);
void Utf16Action(TacticalInputResult actual,string kind)
{
    Check(actual.Request?.Action is not null,"UTF16 action exists " + kind);
    Check(EcmaJson.DeepEquals(EcmaJson.Parse(actual.Request!.Action!.Value.GetRawText()),utf16[kind]),"source UTF16 action " + kind);
    controller.CompleteDispatch(stamp,fixture);
}
controller.Bind(fixture);controller.Click(stamp,new(0,0));controller.Click(stamp,new(2,0));
fixture.CommandsOverride = new[] { new TacticalCommand("utf16", Strange("ability"), Strange("weapon"), false, true, new[] {new BoardCell(3,0),new BoardCell(4,0)}) };
controller.ChooseCommand(stamp,"utf16");Utf16Action(controller.Click(stamp,new(3,0)),"command");
fixture.CommandsOverride=null;
fixture.TacticsOverride = new[] {new TacticalTactic(Strange("commander"),Strange("tactic"),new[]{new BoardCell(3,0),new BoardCell(4,0)})};
controller.ChooseTactic(stamp,Strange("commander")+":"+Strange("tactic"));Utf16Action(controller.Click(stamp,new(3,0)),"tactic");
controller.Click(stamp,new(2,0));Utf16Action(controller.Recruit(stamp,Strange("type")),"recruit");
fixture.Phase="deployment";fixture.HumanTurn=false;
fixture.UnitsOverride=fixture.Root.GetProperty("deploymentPicks").EnumerateArray().Select(row=>new TacticalUnit(row.GetProperty("id").GetInt32(),Fixture.Cell(row.GetProperty("at")),false,true)).ToArray();
fixture.DeploymentOverride=fixture.UnitsOverride.Select(unit=>unit.Id).ToArray();
controller.Bind(fixture);
foreach(var row in fixture.Root.GetProperty("deploymentPicks").EnumerateArray())
{
    var picked=controller.PickDeploymentUnit(stamp,row.GetProperty("id").GetInt32());
    Equal(controller.Selection.Kind.ToString(),row.GetProperty("kind").GetString(),"source roster selection kind");
    Equal(controller.Selection.UnitId,row.GetProperty("selected").GetInt32(),"source roster selection id");
    Equal(controller.InspectId,controller.Selection.UnitId,"roster inspect");
    Check(picked.Request is null && row.GetProperty("dispatches").GetInt32()==0,"roster selection does not swap or dispatch");
    Check(picked.Center && picked.Reveal==Fixture.Cell(row.GetProperty("at")),"roster center hint");
    var painted=controller.Overlay();var expectedPaint=row.GetProperty("view");
    Equal(painted.Selected,CellOrNull(expectedPaint.GetProperty("selected")),"source deployment picked ring");
    Equal(painted.Move,Fixture.Cells(expectedPaint.GetProperty("move")),"source deployment commandable paint gate");
    Equal(painted.Threat,Fixture.Cells(expectedPaint.GetProperty("threat")),"source deployment inspection suppression");
}
controller.SetBusy(true);Check(controller.PickDeploymentUnit(stamp,fixture.DeploymentOverride[0]).Status==TacticalInputStatus.Busy,"busy roster pick");controller.SetBusy(false);
Check(controller.PickDeploymentUnit(stamp,999).Status==TacticalInputStatus.Ignored,"invalid roster pick");
fixture.Phase="playing";fixture.HumanTurn=true;
Check(controller.PickDeploymentUnit(stamp,fixture.DeploymentOverride[0]).Status==TacticalInputStatus.Ignored,"playing roster pick");
fixture.UnitsOverride=null;fixture.DeploymentOverride=null;
fixture.Stamp = stamp with { Revision=1 };
Check(controller.Click(stamp,new(0,0)).Status==TacticalInputStatus.StaleRevision,"adapter advanced revision");
controller.Bind(fixture);
Check(controller.CompleteDispatch(stamp,fixture).Status==TacticalInputStatus.StaleRevision,"old completion");
Console.WriteLine($"Tactical selection: {checks} checks passed; real TypeScript engine/selection differential plus controller contracts.");

sealed class Fixture : ITacticalSelectionQueries
{
    public JsonElement Root { get; }
    public Fixture(JsonElement root) => Root=root;
    public TacticalStamp Stamp {get;set;} = new(1,0);
    public string Phase {get;set;}="playing";
    public bool HumanInput {get;set;}=true;
    public bool HumanTurn {get;set;}=true;
    public bool CanUndo => true;
    public IReadOnlyList<BoardCell>? StopsOverride {get;set;}
    public TacticalAttack? AttackOverride {get;set;}
    public IReadOnlyList<TacticalUnit>? UnitsOverride {get;set;}
    public IReadOnlyList<int>? DeploymentOverride {get;set;}
    public IReadOnlyList<BoardCell>? StepsOverride {get;set;}
    public IReadOnlyList<TacticalCommand>? CommandsOverride {get;set;}
    public IReadOnlyList<TacticalTactic> TacticsOverride {get;set;}=Array.Empty<TacticalTactic>();
    public IReadOnlyList<TacticalPassenger> PassengersOverride {get;set;}=Array.Empty<TacticalPassenger>();
    static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true};
    public static BoardCell Cell(JsonElement at)=>new(at.GetProperty("x").GetInt32(),at.GetProperty("y").GetInt32());
    public static IReadOnlyList<BoardCell> Cells(JsonElement cells)=>cells.ValueKind==JsonValueKind.Null?Array.Empty<BoardCell>():cells.EnumerateArray().Select(Cell).ToArray();
    static string Key(BoardCell at)=>$"{at.X},{at.Y}";
    JsonElement Data(int id)=>Root.GetProperty("units").EnumerateArray().First(u=>u.GetProperty("id").GetInt32()==id);
    public TacticalUnit? Unit(int id) { if(UnitsOverride is not null)return UnitsOverride.FirstOrDefault(unit=>unit.Id==id); var data=Root.GetProperty("units").EnumerateArray().FirstOrDefault(u=>u.GetProperty("id").GetInt32()==id);return data.ValueKind==JsonValueKind.Undefined?null:new(id,Cell(data.GetProperty("at")),data.GetProperty("canAct").GetBoolean(),true); }
    public TacticalUnit? UnitAt(BoardCell at)=>UnitsOverride is not null?UnitsOverride.FirstOrDefault(unit=>unit.At==at&&unit.Visible):Root.GetProperty("units").EnumerateArray().Where(u=>Cell(u.GetProperty("at"))==at).Select(u=>Unit(u.GetProperty("id").GetInt32())).FirstOrDefault();
    public bool AreEnemies(int firstUnit,int secondUnit)=>Root.GetProperty("enemies").EnumerateArray().Any(value=>value.GetString()==$"{firstUnit}:{secondUnit}");
    public bool IsEnemyOfViewer(int unit)=>Data(unit).GetProperty("owner").GetInt32()!=1;
    public bool CanOpenRecruitment(BoardCell at)=>Cells(Root.GetProperty("recruits")).Contains(at);
    public IReadOnlyList<int> Actors()=>Root.GetProperty("actors").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
    public IReadOnlyList<BoardCell> MoveStops(int unit)=>StopsOverride??Cells(Data(unit).GetProperty("stops"));
    public IReadOnlyList<BoardCell>? PathTo(int unit,BoardCell at)=>Data(unit).GetProperty("paths").TryGetProperty(Key(at),out var path)?Cells(path):null;
    public IReadOnlyList<BoardCell> Threat(int unit)=>Cells(Data(unit).GetProperty("threat"));
    public IReadOnlyList<BoardCell> ControlZone(int unit)=>Cells(Data(unit).GetProperty("control"));
    public IReadOnlyList<TacticalCommand> CommandsAt(int unit,BoardCell at)=>CommandsOverride??JsonSerializer.Deserialize<TacticalCommand[]>(Data(unit).GetProperty("commands").GetProperty(Key(at)),JsonOptions)!;
    public TacticalAttack Attack(int unit,BoardCell target,BoardCell from,string weapon)=>AttackOverride??JsonSerializer.Deserialize<TacticalAttack>(Data(unit).GetProperty("attacks").GetProperty($"{Key(from)}:{Key(target)}:{weapon}"),JsonOptions)!;
    public IReadOnlyList<int> DeploymentUnits()=>DeploymentOverride??Actors();
    public IReadOnlyList<BoardCell> DeploymentZone()=>new[]{new BoardCell(0,0),new BoardCell(1,0)};
    public IReadOnlyList<BoardCell> DeploymentSpots(int unit)=>DeploymentZone();
    public IReadOnlyList<TacticalTactic> Tactics()=>TacticsOverride;
    public IReadOnlyList<TacticalPassenger> Passengers(int carrier)=>PassengersOverride;
    public IReadOnlyList<BoardCell> DirectionSteps(BoardCell from)=>StepsOverride??Cells(Root.GetProperty("steps").GetProperty(Key(from)));
}
