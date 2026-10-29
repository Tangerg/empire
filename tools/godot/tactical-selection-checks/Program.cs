using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Determinism;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Presentation.Tactical;

var fixture = new Fixture(JsonDocument.Parse(File.ReadAllText(args[0])).RootElement.Clone());
var controller = new TacticalSelectionController(fixture);
int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new InvalidOperationException(name); }
void Equal<T>(T actual, T expected, string name) => Check(JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(expected), name + $" actual={JsonSerializer.Serialize(actual)} expected={JsonSerializer.Serialize(expected)}");
// Metadata admission does not depend on a historical battle-content schema.
try
{
    new AbilityRegistry().Register(new("invalid-target", (AbilityTargetKind)999));
    throw new InvalidOperationException("Unknown ability target kind was admitted");
}
catch (DomainInvariantException) { checks++; }
try
{
    DefaultAbilities.Create().Replace(new("attack", (AbilityTargetKind)999));
    throw new InvalidOperationException("Unknown replacement target kind was admitted");
}
catch (DomainInvariantException) { checks++; }
var defaultAbilities = DefaultAbilities.Create();
Equal(defaultAbilities.Get("attack").TargetKind, AbilityTargetKind.UnitOrStructure, "attack click subject metadata");
Equal(defaultAbilities.Get("heal").TargetKind, AbilityTargetKind.Unit, "heal click subject metadata");
Equal(defaultAbilities.Get("capture").TargetKind, AbilityTargetKind.Self, "capture click subject metadata");
Equal(defaultAbilities.Get("wait").TargetKind, AbilityTargetKind.Self, "wait click subject metadata");
BoardCell? CellOrNull(JsonElement element) => element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : Fixture.Cell(element);
foreach (var entry in fixture.Root.GetProperty("trace").EnumerateArray())
{
    var op = entry.GetProperty("op");
    var name = op[0].GetString();
    var beforeSelection = controller.Selection;
    var result = name switch
    {
        "back" => controller.Back(controller.Stamp),
        "hover" => controller.Hover(controller.Stamp, fixture.SourceHit(new(op[1].GetInt32(), op[2].GetInt32()))),
        _ => controller.Click(controller.Stamp, fixture.SourceHit(new(op[1].GetInt32(), op[2].GetInt32()))),
    };
    var expectedAction = entry.GetProperty("action");
    // Native confirmation is an intentional UI delta, not a battle-rule delta.
    // Compare the original canonical action after the now-required explicit order.
    if (expectedAction.ValueKind != JsonValueKind.Null && result.Request is null)
    {
        Check(controller.Selection.Kind == TacticalSelectionKind.Destination, "sole command first opens destination preview");
        result = controller.ChooseCommand(controller.Stamp, expectedAction.GetProperty("command").GetProperty("ability").GetString()!);
    }
    if (result.Status == TacticalInputStatus.InvalidTarget)
    {
        Equal(controller.Selection, beforeSelection, "invalid target retains the complete preview");
        Check(result.Request is null, "invalid target cannot emit an order");
        // The old trace canceled on a miss. Explicitly cancel before replaying
        // its remaining independent operations against unchanged engine facts.
        while (controller.Selection.Kind != TacticalSelectionKind.Idle)
            controller.Back(controller.Stamp);
    }
    Check(JsonNode.DeepEquals(result.Request?.Action is { } action ? JsonNode.Parse(action.GetRawText()) : null,
        expectedAction.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(expectedAction.GetRawText())), $"source action {op}");
    if (result.Request is not null)
        controller.CompleteDispatch(controller.Stamp, fixture);
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
var defaultCells = fixture.InteractiveCells;
foreach (var entry in fixture.Root.GetProperty("overlays").EnumerateArray())
{
    if (entry.GetProperty("hidden").GetBoolean())
    {
        fixture.UnitsOverride = new[] { new TacticalUnit(1, new(0, 0), true, true), new TacticalUnit(2, new(19, 0), false, false) };
        fixture.InteractiveCells = Enumerable.Range(0, 40).Select(index => new BoardCell(index % 20, index / 20)).ToFrozenSet();
    }
    controller.Bind(fixture);
    if (entry.GetProperty("picked").GetBoolean())
        controller.Click(controller.Stamp, fixture.SourceHit(new(0, 0)));
    controller.SetBusy(entry.GetProperty("busy").GetBoolean());
    controller.Hover(controller.Stamp, fixture.SourceHit(Fixture.Cell(entry.GetProperty("hover"))));
    var expected = entry.GetProperty("view");
    var overlay = controller.Overlay();
    var name = entry.GetProperty("name").GetString();
    Equal(overlay.Selected, CellOrNull(expected.GetProperty("selected")), "full source overlay selected " + name);
    Equal(overlay.Cursor, CellOrNull(expected.GetProperty("cursor")), "full source overlay cursor " + name);
    Equal(overlay.Move, Fixture.Cells(expected.GetProperty("move")), "full source overlay move " + name);
    Equal(overlay.Attack, Fixture.Cells(expected.GetProperty("attack")), "full source overlay attack " + name);
    Equal(overlay.Helpful, Fixture.Cells(expected.GetProperty("helpful")), "full source overlay helpful " + name);
    Equal(overlay.Controlled, Fixture.Cells(expected.GetProperty("controlled")), "full source overlay control " + name);
    Equal(overlay.Path, Fixture.Cells(expected.GetProperty("path")), "full source overlay path " + name);
    Equal(overlay.Threat, Fixture.Cells(expected.GetProperty("threat")), "full source overlay inspected threat " + name);
    fixture.UnitsOverride = null;
    fixture.InteractiveCells = defaultCells;
}
controller.Bind(fixture);
var nativeKeyboardCursors = new BoardCell?[] { new(0, 0), new(1, 0), new(1, 0), new(1, 0), new(1, 0), new(1, 0),
    new(0, 0), new(1, 0), new(1, 0), new(1, 0), new(1, 0) };
var keyboardIndex = 0;
foreach (var entry in fixture.Root.GetProperty("keyboard").EnumerateArray())
{
    var op = entry.GetProperty("op");
    var result = op[0].GetString() == "key" ? controller.Key(controller.Stamp, op[1].GetString()!)
        : controller.Click(controller.Stamp, fixture.SourceHit(new(op[1].GetInt32(), op[2].GetInt32())));
    var expectedAction = entry.GetProperty("action");
    Check(JsonNode.DeepEquals(result.Request?.Action is { } action ? JsonNode.Parse(action.GetRawText()) : null,
        expectedAction.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(expectedAction.GetRawText())), $"source controller action {op}");
    if (result.Request is not null)
        controller.CompleteDispatch(controller.Stamp, fixture);
    Equal(controller.Selection.Kind.ToString(), keyboardIndex == 10 ? "Idle" : entry.GetProperty("kind").GetString(), $"native keyboard selection {op}");
    Equal(controller.Selection.UnitId, entry.GetProperty("unitId").ValueKind == JsonValueKind.Null
        ? (int?)null : entry.GetProperty("unitId").GetInt32(), $"source keyboard actor {op}");
    Equal(controller.Cursor, nativeKeyboardCursors[keyboardIndex++], $"native keyboard cursor {op}");
    Equal(result.PreventDefault, entry.GetProperty("prevented").GetBoolean(), $"source preventDefault {op}");
}
controller.Bind(fixture);
var hoverLeave = fixture.Root.GetProperty("hoverLeave");
controller.SetBusy(true);
controller.Hover(controller.Stamp, fixture.SourceHit(new(3, 0)));
Equal(controller.Cursor, CellOrNull(hoverLeave.GetProperty("hover").GetProperty("cursor")), "source busy hover cursor");
Equal(controller.InspectId, hoverLeave.GetProperty("hover").GetProperty("inspect").GetInt32(), "source busy hover inspection");
controller.Leave(controller.Stamp);
Equal(controller.Cursor, CellOrNull(hoverLeave.GetProperty("leave").GetProperty("cursor")), "source leave clears cursor");
Equal(controller.InspectId, hoverLeave.GetProperty("leave").GetProperty("inspect").GetInt32(), "source busy leave preserves inspection");
Check(controller.Leave(controller.Stamp with { Binding = 99 }).Status == TacticalInputStatus.StaleBinding, "stale leave binding");
controller.Bind(fixture);
var stamp = controller.Stamp;
Check(controller.Click(stamp with { Binding = 99 }, fixture.SourceHit(new(0, 0))).Status == TacticalInputStatus.StaleBinding, "stale binding");
Check(controller.Click(stamp with { Revision = 99 }, fixture.SourceHit(new(0, 0))).Status == TacticalInputStatus.StaleRevision, "stale revision");
controller.SetBusy(true);
Check(controller.Click(stamp, fixture.SourceHit(new(0, 0))).Status == TacticalInputStatus.Busy, "busy click");
Check(controller.Key(stamp, "Escape").Status == TacticalInputStatus.Busy, "busy escape");
Check(controller.Overlay().Move.Count == 0, "busy overlays");
controller.SetBusy(false);
fixture.HumanTurn = false;
fixture.HumanInput = false;
Check(controller.Key(stamp, "e").Status == TacticalInputStatus.NotHuman, "AI end turn");
Check(controller.Back(stamp).Status == TacticalInputStatus.Ready, "AI escape precedence");
fixture.HumanTurn = true;
fixture.HumanInput = true;
fixture.Phase = "over";
Check(controller.Click(stamp, fixture.SourceHit(new(0, 0))).Status == TacticalInputStatus.Terminal, "terminal click");
fixture.Phase = "playing";
controller.Bind(fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
controller.Click(stamp, fixture.SourceHit(new(2, 0)));
var wait = controller.Key(stamp, "w");
Check(wait.Request?.Action?.GetProperty("command").GetProperty("ability").GetString() == "wait", "command before WASD");
Check(controller.Cursor is null, "command does not move cursor");
controller.CompleteDispatch(stamp, fixture);
controller.Key(stamp, " ");
Equal(controller.Selection.UnitId, fixture.Actors()[0], "cycle source actor order");
controller.Hover(stamp, fixture.SourceHit(new(0, 0)));
fixture.StepsOverride = new[] { new BoardCell(1, 1), new BoardCell(1, 0) };
controller.Key(stamp, "d");
Equal(controller.Cursor, new BoardCell(1, 1), "configured direction strict tie");
fixture.StepsOverride = null;
controller.Bind(fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
controller.Click(stamp, fixture.SourceHit(new(2, 0)));
var singletonOption = fixture.CommandsAt(1, new(2, 0)).Single(option => option.Key == "attack:soldier_sword");
Check(singletonOption.TargetSubjects.Single() == TacticalTarget.Unit(new(3, 0), 2),
    "singleton command carries canonical Unit subject metadata");
var single = controller.ChooseCommand(stamp, "attack");
Equal(Fixture.Cell(single.Request!.Action!.Value.GetProperty("command").GetProperty("target")), singletonOption.Targets.Single(),
    "explicit HUD singleton command preserves its original canonical target coordinate");
Check(single.Request?.Action?.GetProperty("command").TryGetProperty("target", out _) == true, "single target auto dispatch");
controller.CompleteDispatch(stamp, fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
controller.Click(stamp, fixture.SourceHit(new(2, 0)));
fixture.CommandsOverride = new[] { new TacticalCommand("blessing", "customHelp", null, false, false, new[] { TacticalTarget.AtCell(new(0, 0)), TacticalTarget.AtCell(new(1, 0)) }) };
controller.ChooseCommand(stamp, "blessing");
Equal(controller.Selection.Kind, TacticalSelectionKind.Target, "multiple targets arm");
Equal(controller.Overlay().Helpful.Count, 2, "engagement metadata helpful");
Equal(controller.Overlay().Attack.Count, 0, "engagement not ability ID");
var previewHit = TacticalHit.Ground(new(0, 0));
controller.Hover(stamp, previewHit);
Equal(controller.Selection.PreviewTarget, previewHit, "valid target hover owns the explicit preview");
controller.Hover(stamp, TacticalHit.Ground(new(2, 0)));
Equal(controller.Selection.PreviewTarget, previewHit, "ordinary ground traversal preserves preview for HUD inspection");
Equal(controller.CursorHit, TacticalHit.Ground(new(2, 0)), "activation cursor remains the actual ground, never a preview alias");
var groundEnter = controller.Key(stamp, "enter");
Check(groundEnter.Request is null && groundEnter.Status == TacticalInputStatus.InvalidTarget, "pinned preview does not authorize Enter at a different ground cell");
controller.Leave(stamp);
Check(controller.Selection.PreviewTarget is null && controller.CursorHit is null, "leaving the input context clears both cursor and preview");
controller.Hover(stamp, previewHit);
controller.Back(stamp);
Check(controller.Selection.PreviewTarget is null, "cancel clears preview when returning to destination");
Equal(controller.Selection.Kind, TacticalSelectionKind.Destination, "target back");
controller.Back(stamp);
Equal(controller.Selection.Kind, TacticalSelectionKind.Unit, "destination back");
controller.Back(stamp);
Equal(controller.Selection.Kind, TacticalSelectionKind.Idle, "unit back");
fixture.CommandsOverride = null;
fixture.TacticsOverride = new[] { new TacticalTactic("c", "t", new[] { new BoardCell(0, 0), new BoardCell(1, 0) }) };
controller.ChooseTactic(stamp, "c:t");
Equal(controller.Selection.Kind, TacticalSelectionKind.Tactic, "tactic arm");
Check(controller.Click(stamp, fixture.SourceHit(new(1, 0))).Request?.Action?.GetProperty("kind").GetString() == "tactic", "tactic dispatch");
controller.CompleteDispatch(stamp, fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
fixture.PassengersOverride = new[] { new TacticalPassenger(9, new[] { new BoardCell(1, 0) }) };
controller.ChooseDisembark(stamp, 9);
Equal(controller.Selection.Kind, TacticalSelectionKind.Disembark, "disembark arm");
Equal(controller.Overlay().Helpful.Count, 1, "disembark helpful");
controller.Back(stamp);
Equal(controller.Selection.Kind, TacticalSelectionKind.Idle, "disembark source default back");
fixture.Phase = "deployment";
fixture.HumanTurn = false;
controller.Bind(fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
Equal(controller.Selection.Kind, TacticalSelectionKind.DeploymentUnit, "deployment pick");
Check(controller.Click(stamp, fixture.SourceHit(new(1, 0))).Request?.Action?.GetProperty("kind").GetString() == "deployUnit", "deployment dispatch");
controller.CompleteDispatch(stamp, fixture);
Equal(controller.Selection.Kind, TacticalSelectionKind.Deploying, "deployment settles");
Check(controller.Key(stamp, "arrowright").Status == TacticalInputStatus.NotHuman, "source keyboard deployment gate");
fixture.Phase = "playing";
fixture.HumanTurn = true;
controller.Bind(fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
// A legal refusal resets composition just as a successful settled request does.
controller.SetBusy(true);
controller.CompleteDispatch(stamp, fixture);
Equal(controller.Selection.Kind, TacticalSelectionKind.Idle, "failed action reset");
// Equal scores must retain first stop and first option, including weapon key order.
fixture.StopsOverride = new[] { new BoardCell(2, 0), new BoardCell(2, 1) };
fixture.CommandsOverride = new[] {
    new TacticalCommand("z", "volley", "zWeapon", false, true, new[] { TacticalTarget.Unit(new(3, 0), 2) }),
    new TacticalCommand("a", "volley", "aWeapon", false, true, new[] { TacticalTarget.Unit(new(3, 0), 2) }) };
fixture.AttackOverride = new TacticalAttack(10, 0, 0, 0, true, new[] { new BoardCell(3, 0) });
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
controller.Click(stamp, fixture.SourceHit(new(3, 0)));
Equal(controller.Selection.Destination, new BoardCell(2, 0), "best spot strict tie preserves stops");
Equal(controller.Selection.Weapon, "zWeapon", "best spot preserves option order");
Equal(controller.Selection.Ability, "volley", "best spot preserves custom ability");
fixture.StopsOverride = null;
fixture.CommandsOverride = null;
fixture.AttackOverride = null;
// Compare escaped UTF-16 code units via the canonical boundary, not GetString/DeepEquals.
var utf16 = EcmaJson.Parse(fixture.Root.GetProperty("utf16").GetRawText())!.AsObject();
string Strange(string key) => EcmaJson.ReadString(utf16["strange"]![key]);
void Utf16Action(TacticalInputResult actual, string kind)
{
    Check(actual.Request?.Action is not null, "UTF16 action exists " + kind);
    Check(EcmaJson.DeepEquals(EcmaJson.Parse(actual.Request!.Action!.Value.GetRawText()), utf16[kind]), "source UTF16 action " + kind);
    controller.CompleteDispatch(stamp, fixture);
}
controller.Bind(fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
controller.Click(stamp, fixture.SourceHit(new(2, 0)));
fixture.CommandsOverride = new[] { new TacticalCommand("utf16", Strange("ability"), Strange("weapon"), false, true, new[] { TacticalTarget.Unit(new(3, 0), 2), TacticalTarget.AtCell(new(4, 0)) }) };
controller.ChooseCommand(stamp, "utf16");
Utf16Action(controller.Click(stamp, fixture.SourceHit(new(3, 0))), "command");
fixture.CommandsOverride = null;
fixture.TacticsOverride = new[] { new TacticalTactic(Strange("commander"), Strange("tactic"), new[] { new BoardCell(3, 0), new BoardCell(4, 0) }) };
controller.ChooseTactic(stamp, Strange("commander") + ":" + Strange("tactic"));
Utf16Action(controller.Click(stamp, fixture.SourceHit(new(3, 0))), "tactic");
controller.Click(stamp, fixture.SourceHit(new(2, 0)));
Utf16Action(controller.Recruit(stamp, Strange("type")), "recruit");
fixture.Phase = "deployment";
fixture.HumanTurn = false;
fixture.UnitsOverride = fixture.Root.GetProperty("deploymentPicks").EnumerateArray().Select(row => new TacticalUnit(row.GetProperty("id").GetInt32(), Fixture.Cell(row.GetProperty("at")), false, true)).ToArray();
fixture.DeploymentOverride = fixture.UnitsOverride.Select(unit => unit.Id).ToArray();
controller.Bind(fixture);
foreach (var row in fixture.Root.GetProperty("deploymentPicks").EnumerateArray())
{
    var picked = controller.PickDeploymentUnit(stamp, row.GetProperty("id").GetInt32());
    Equal(controller.Selection.Kind.ToString(), row.GetProperty("kind").GetString(), "source roster selection kind");
    Equal(controller.Selection.UnitId, row.GetProperty("selected").GetInt32(), "source roster selection id");
    Equal(controller.InspectId, controller.Selection.UnitId, "roster inspect");
    Check(picked.Request is null && row.GetProperty("dispatches").GetInt32() == 0, "roster selection does not swap or dispatch");
    Check(picked.Center && picked.Reveal == Fixture.Cell(row.GetProperty("at")), "roster center hint");
    var painted = controller.Overlay();
    var expectedPaint = row.GetProperty("view");
    Equal(painted.Selected, CellOrNull(expectedPaint.GetProperty("selected")), "source deployment picked ring");
    Equal(painted.Move, Fixture.Cells(expectedPaint.GetProperty("move")), "source deployment commandable paint gate");
    Equal(painted.Threat, Fixture.Cells(expectedPaint.GetProperty("threat")), "source deployment inspection suppression");
}
controller.SetBusy(true);
Check(controller.PickDeploymentUnit(stamp, fixture.DeploymentOverride[0]).Status == TacticalInputStatus.Busy, "busy roster pick");
controller.SetBusy(false);
Check(controller.PickDeploymentUnit(stamp, 999).Status == TacticalInputStatus.Ignored, "invalid roster pick");
fixture.Phase = "playing";
fixture.HumanTurn = true;
Check(controller.PickDeploymentUnit(stamp, fixture.DeploymentOverride[0]).Status == TacticalInputStatus.Ignored, "playing roster pick");
fixture.UnitsOverride = null;
fixture.DeploymentOverride = null;
// Cell eligibility is canonical even when a core movement/target query includes that cell.
controller.Bind(fixture);
controller.Click(stamp, fixture.SourceHit(new(0, 0)));
fixture.InteractiveCells = defaultCells.Where(cell => cell != new BoardCell(3, 0)).ToFrozenSet();
var beforeOutside = controller.Selection;
Check(controller.Click(stamp, fixture.SourceHit(new(3, 0))).Status == TacticalInputStatus.Ignored, "outside click is ignored");
Equal(controller.Selection, beforeOutside, "outside click preserves selection");
controller.Hover(stamp, fixture.SourceHit(new(1, 0)));
controller.Hover(stamp, fixture.SourceHit(new(3, 0)));
Check(controller.Cursor is null && controller.InspectId != 2, "outside hover cannot inspect or retain cursor");
controller.Hover(stamp, fixture.SourceHit(new(0, 0)));
fixture.StepsOverride = new[] { new BoardCell(3, 0), new BoardCell(1, 0) };
var boundedStep = controller.Key(stamp, "arrowright");
Check(controller.Cursor == new BoardCell(1, 0) && boundedStep.Reveal == controller.Cursor,
    "keyboard skips ineligible direction before scoring and revealing");
fixture.InteractiveCells = defaultCells.Where(cell => cell != new BoardCell(0, 0)).ToFrozenSet();
controller.Bind(fixture);
var cycleOutside = controller.Key(stamp, " ");
Check(controller.Selection.UnitId != 1 && cycleOutside.Reveal != new BoardCell(0, 0), "keyboard cycle excludes outside actor");
fixture.InteractiveCells = defaultCells;
fixture.StepsOverride = null;
controller.Bind(fixture);
Check(controller.Click(stamp, fixture.SourceHit(new(-1, 0))).Status == TacticalInputStatus.Ignored, "out-of-bounds click is ignored");
controller.Hover(stamp, fixture.SourceHit(new(0, 0)));
fixture.InteractiveCells = defaultCells.Where(cell => cell != new BoardCell(0, 0)).ToFrozenSet();
controller.CompleteDispatch(stamp, fixture);
Check(controller.Cursor is null, "settled revision cannot retain an ineligible cursor");
fixture.InteractiveCells = defaultCells;
// Rendered Ground/Object must never be promoted using the canonical occupant at the same cell.
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Ground(new(0, 0)));
Check(controller.Selection.Kind == TacticalSelectionKind.Idle && controller.InspectId is null,
    "Ground over a canonically visible actor cannot select or inspect it");
controller.Hover(stamp, TacticalHit.Unit(new(3, 0), 2));
Check(controller.InspectId == 2, "visible Unit hover inspects the exact subject");
controller.Hover(stamp, TacticalHit.Ground(new(3, 0)));
Check(controller.InspectId is null && controller.Cursor == new BoardCell(3, 0),
    "same-cell Ground hover clears concealed entity inspection");
fixture.Sites = new HashSet<BoardCell> { new(3, 0) };
controller.Click(stamp, TacticalHit.Object(new(3, 0)));
Check(controller.Selection.Kind == TacticalSelectionKind.Site && controller.InspectId is null,
    "Object with an occupant selects the site, not its concealed occupant");
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
controller.Hover(stamp, TacticalHit.Unit(new(3, 0), 2));
controller.Hover(stamp, TacticalHit.Object(new(3, 0)));
Check(controller.InspectId == 1, "Object hover restores selected inspection instead of retaining a concealed enemy");
controller.Click(stamp, TacticalHit.Ground(new(3, 0)));
Check(controller.Selection.Kind == TacticalSelectionKind.Unit && controller.InspectId == 1,
    "Ground over enemy preserves selection without inspecting or auto-arming the concealed actor");
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
controller.Click(stamp, TacticalHit.Object(new(3, 0)));
Check(controller.Selection.Kind == TacticalSelectionKind.Site && controller.InspectId is null,
    "Object over enemy cannot auto-arm an attack");
Check(controller.Click(stamp, TacticalHit.Unit(new(0, 0), 2)).Status == TacticalInputStatus.Ignored,
    "Unit hit identity must match its canonical cell");
fixture.Sites = new HashSet<BoardCell>();

void ArmTypedCommand(params TacticalTarget[] targets)
{
    controller.Bind(fixture);
    fixture.CommandsOverride = null;
    controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
    controller.Click(stamp, TacticalHit.Ground(new(2, 0)));
    fixture.CommandsOverride = new[] { new TacticalCommand("typed", "custom-subject", null, false, false, targets) };
    controller.ChooseCommand(stamp, "typed");
}
var entityTargets = new[] { TacticalTarget.Unit(new(3, 0), 2), TacticalTarget.Unit(new(0, 0), 1) };
foreach (var concealed in new[] { TacticalHit.Ground(new(3, 0)), TacticalHit.Object(new(3, 0)) })
{
    ArmTypedCommand(entityTargets);
    controller.Hover(stamp, concealed);
    Check(controller.HoverTarget is null, "concealed entity has no typed target hover");
    Check(controller.Click(stamp, concealed).Request is null, "Ground/Object cannot dispatch a Unit-target command");
}
ArmTypedCommand(entityTargets);
controller.Hover(stamp, TacticalHit.Unit(new(3, 0), 2));
Check(controller.HoverTarget == new BoardCell(3, 0), "visible entity target hover");
Check(controller.Click(stamp, TacticalHit.Unit(new(3, 0), 2)).Request is not null, "explicit Unit dispatches entity target");
ArmTypedCommand(TacticalTarget.Object(new(3, 0)), TacticalTarget.Object(new(4, 0)));
Check(controller.Click(stamp, TacticalHit.Ground(new(3, 0))).Request is null, "Ground cannot impersonate a structure target");
ArmTypedCommand(TacticalTarget.Object(new(3, 0)), TacticalTarget.Object(new(4, 0)));
Check(controller.Click(stamp, TacticalHit.Object(new(3, 0))).Request is not null, "explicit Object dispatches structure target");
ArmTypedCommand(TacticalTarget.AtCell(new(3, 0)), TacticalTarget.AtCell(new(4, 0)));
Check(controller.Click(stamp, TacticalHit.Ground(new(3, 0))).Request is not null,
    "custom Cell target remains legal at a concealed occupant without becoming an entity selection");
ArmTypedCommand(TacticalTarget.AtCell(new(4, 0)), TacticalTarget.AtCell(new(5, 0)));
var objectCellAction = controller.Click(stamp, TacticalHit.Object(new(4, 0))).Request?.Action;
Check(objectCellAction?.GetProperty("command").GetProperty("target").GetProperty("x").GetInt32() == 4,
    "Cell target uses explicit Object logical location, not inferred ground beneath its silhouette");
ArmTypedCommand(entityTargets);
controller.Hover(stamp, TacticalHit.Ground(new(3, 0)));
Check(controller.Key(stamp, "enter").Request is null, "Enter preserves the current pointer Ground subject");
fixture.CommandsOverride = null;
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
controller.Hover(stamp, TacticalHit.Ground(new(2, 0)));
var focused = controller.Key(stamp, "arrowright");
Check(focused.CursorFocused && controller.CursorHit == TacticalHit.Unit(new(3, 0), 2), "keyboard navigation explicitly declares and focuses canonical Unit");
controller.Bind(fixture);
controller.Hover(stamp, TacticalHit.Ground(new(3, 0)));
var cycled = controller.Key(stamp, "tab");
Check(cycled.CursorFocused && controller.CursorHit == TacticalHit.Unit(new(0, 0), 1) && controller.Selection.UnitId == 1,
    "cycle explicitly focuses the selected actor instead of retaining an unrelated pointer cursor");
var ownPreview = controller.Key(stamp, "enter");
Check(ownPreview.Request is null && controller.Selection.Kind == TacticalSelectionKind.Destination
    && controller.Selection.Destination == new BoardCell(0, 0), "Enter after cycle previews the selected actor's own cell without waiting");
controller.Back(stamp);
controller.Key(stamp, "arrowright");
Check(controller.Cursor == new BoardCell(1, 0), "movement after cycle starts at the selected actor, not the old pointer");
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
fixture.CommandsOverride = new[] { new TacticalCommand("wait", "wait", null, true, false, Array.Empty<TacticalTarget>()) };
var repeatedSelf = controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
Check(repeatedSelf.Request is null && controller.Selection.Kind == TacticalSelectionKind.Destination,
    "clicking an already-selected actor cannot silently spend its turn when wait is the only order");
Check(controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1)).Request is null,
    "repeated clicks on that destination still cannot commit an order");
Check(controller.ChooseCommand(stamp, "wait").Request?.Action?.GetProperty("command").GetProperty("ability").GetString() == "wait",
    "explicit wait remains available through the canonical command path");
fixture.CommandsOverride = new[] { new TacticalCommand("wait", "wait", null, true, false, Array.Empty<TacticalTarget>()) };
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
var originWait = controller.ChooseCommand(stamp, "wait");
Check(controller.Selection.Kind == TacticalSelectionKind.Unit && originWait.Request?.Action?.GetProperty("command").GetProperty("ability").GetString() == "wait",
    "the explicit origin Wait button dispatches directly from selected Unit mode");
Equal(Fixture.Cells(originWait.Request!.Action!.Value.GetProperty("path")), fixture.PathTo(1, new(0, 0)),
    "origin orders reuse the canonical own-cell path");
fixture.CommandsOverride = new[] { new TacticalCommand("origin-attack", "attack", "soldier_sword", false, true, new[] { TacticalTarget.Unit(new(3, 0), 2) }) };
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
var originAttack = controller.ChooseCommand(stamp, "origin-attack");
Check(originAttack.Request is null && controller.Selection.Kind == TacticalSelectionKind.Target
    && controller.Selection.Destination == new BoardCell(0, 0),
    "an origin attack button arms its canonical targets without auto-attacking a singleton");
Check(controller.Selection.TargetSubjects.Single() == TacticalTarget.Unit(new(3, 0), 2),
    "origin attack preview preserves the typed target subject");
var originEscape = controller.Key(stamp, "escape");
Check(originEscape.Request is null && controller.Stamp == stamp && controller.Selection.Kind == TacticalSelectionKind.Destination,
    "Escape from origin preview changes only selection, never revision or battle state");
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
fixture.UnitsOverride = new[] { new TacticalUnit(1, new(0, 0), false, true), new TacticalUnit(2, new(3, 0), false, true) };
var actedOrigin = controller.ChooseCommand(stamp, "origin-attack");
Check(actedOrigin.Request is null && actedOrigin.Status == TacticalInputStatus.Ignored && controller.Selection.Kind == TacticalSelectionKind.Unit,
    "a no-longer-actionable selected actor cannot arm or dispatch through the origin button");
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
Check(controller.Selection.Kind == TacticalSelectionKind.Idle && controller.ChooseCommand(stamp, "origin-attack").Request is null,
    "clicking an acted actor is inspection, not a command mode");
fixture.UnitsOverride = null;
fixture.CommandsOverride = null;
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
var beforeMiss = controller.Selection;
var miss = controller.Click(stamp, TacticalHit.Ground(new(5, 1)));
Check(miss.Status == TacticalInputStatus.InvalidDestination && miss.Request is null && controller.Selection == beforeMiss,
    "an unreachable destination returns actionable feedback and retains selection");

fixture.Phase = "deployment";
fixture.HumanTurn = false;
fixture.UnitsOverride = new[] { new TacticalUnit(1, new(0, 0), false, true), new TacticalUnit(2, new(1, 0), false, true) };
fixture.DeploymentOverride = new[] { 1, 2 };
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Ground(new(0, 0)));
Check(controller.Selection.Kind == TacticalSelectionKind.Deploying, "Ground cannot pick a concealed deployment unit");
controller.Hover(stamp, TacticalHit.Ground(new(1, 0)));
var rosterPick = controller.PickDeploymentUnit(stamp, 1);
Check(rosterPick.CursorFocused && controller.CursorHit == TacticalHit.Unit(new(0, 0), 1), "roster explicitly focuses the chosen actor instead of retaining an unrelated pointer cursor");
controller.Bind(fixture);
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
Check(controller.Click(stamp, TacticalHit.Ground(new(1, 0))).Request is null,
    "Ground cannot swap a concealed deployment unit");
controller.Click(stamp, TacticalHit.Unit(new(0, 0), 1));
Check(controller.Click(stamp, TacticalHit.Unit(new(1, 0), 2)).Request is not null,
    "visible deployment Unit retains explicit swap behavior");
fixture.Phase = "playing";
fixture.HumanTurn = true;
fixture.UnitsOverride = null;
fixture.DeploymentOverride = null;
controller.Bind(fixture);
controller.Hover(stamp, TacticalHit.Unit(new(3, 0), 2));
controller.CompleteDispatch(stamp, fixture);
Check(controller.CursorHit == TacticalHit.Unit(new(3, 0), 2) && controller.InspectId == 2,
    "same-revision completion retains the current rendered hit");
fixture.Stamp = stamp with { Revision = 1 };
Check(controller.Click(stamp, fixture.SourceHit(new(0, 0))).Status == TacticalInputStatus.StaleRevision, "adapter advanced revision");
controller.CompleteDispatch(stamp, fixture);
Check(controller.CursorHit is null && controller.InspectId is null,
    "new revision invalidates the previous rendered pixel hit and inspection");
controller.Bind(fixture);
Check(controller.CompleteDispatch(stamp, fixture).Status == TacticalInputStatus.StaleRevision, "old completion");
Console.WriteLine($"Tactical selection: {checks} checks passed; frozen source operation comparisons plus explicit native cursor/subject contracts.");

sealed class Fixture : ITacticalSelectionQueries
{
    public JsonElement Root
    {
        get;
    }
    public Fixture(JsonElement root)
    {
        Root = root;
        InteractiveCells = root.GetProperty("steps").EnumerateObject().Select(property =>
        {
            var coordinate = property.Name.Split(',');
            return new BoardCell(int.Parse(coordinate[0]), int.Parse(coordinate[1]));
        }).ToFrozenSet();
    }
    public IReadOnlySet<BoardCell> InteractiveCells
    {
        get; set;
    }
    public TacticalStamp Stamp { get; set; } = new(1, 0);
    public string Phase { get; set; } = "playing";
    public bool HumanInput { get; set; } = true;
    public bool HumanTurn { get; set; } = true;
    public bool CanUndo => true;
    public IReadOnlyList<BoardCell>? StopsOverride
    {
        get; set;
    }
    public TacticalAttack? AttackOverride
    {
        get; set;
    }
    public IReadOnlyList<TacticalUnit>? UnitsOverride
    {
        get; set;
    }
    public IReadOnlyList<int>? DeploymentOverride
    {
        get; set;
    }
    public IReadOnlyList<BoardCell>? StepsOverride
    {
        get; set;
    }
    public IReadOnlyList<TacticalCommand>? CommandsOverride
    {
        get; set;
    }
    public IReadOnlyList<TacticalTactic> TacticsOverride { get; set; } = Array.Empty<TacticalTactic>();
    public IReadOnlyList<TacticalPassenger> PassengersOverride { get; set; } = Array.Empty<TacticalPassenger>();
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static BoardCell Cell(JsonElement at) => new(at.GetProperty("x").GetInt32(), at.GetProperty("y").GetInt32());
    public static IReadOnlyList<BoardCell> Cells(JsonElement cells) => cells.ValueKind == JsonValueKind.Null ? Array.Empty<BoardCell>() : cells.EnumerateArray().Select(Cell).ToArray();
    static string Key(BoardCell at) => $"{at.X},{at.Y}";
    JsonElement Data(int id) => Root.GetProperty("units").EnumerateArray().First(u => u.GetProperty("id").GetInt32() == id);
    public TacticalUnit? Unit(int id)
    {
        if (UnitsOverride is not null)
            return UnitsOverride.FirstOrDefault(unit => unit.Id == id);
        var data = Root.GetProperty("units").EnumerateArray().FirstOrDefault(u => u.GetProperty("id").GetInt32() == id);
        return data.ValueKind == JsonValueKind.Undefined ? null : new(id, Cell(data.GetProperty("at")), data.GetProperty("canAct").GetBoolean(), true);
    }
    public TacticalUnit? UnitAt(BoardCell at) => UnitsOverride is not null ? UnitsOverride.FirstOrDefault(unit => unit.At == at && unit.Visible) : Root.GetProperty("units").EnumerateArray().Where(u => Cell(u.GetProperty("at")) == at).Select(u => Unit(u.GetProperty("id").GetInt32())).FirstOrDefault();
    public bool AreEnemies(int firstUnit, int secondUnit) => Root.GetProperty("enemies").EnumerateArray().Any(value => value.GetString() == $"{firstUnit}:{secondUnit}");
    public bool IsEnemyOfViewer(int unit) => Data(unit).GetProperty("owner").GetInt32() != 1;
    public IReadOnlySet<BoardCell> Sites { get; set; } = new HashSet<BoardCell>();
    public bool IsSite(BoardCell at) => Sites.Contains(at);
    public bool CanOpenRecruitment(BoardCell at) => Cells(Root.GetProperty("recruits")).Contains(at);
    public IReadOnlyList<int> Actors() => Root.GetProperty("actors").EnumerateArray().Select(value => value.GetInt32()).ToArray();
    public IReadOnlyList<BoardCell> MoveStops(int unit) => StopsOverride ?? Cells(Data(unit).GetProperty("stops"));
    public IReadOnlyList<BoardCell>? PathTo(int unit, BoardCell at) => Data(unit).GetProperty("paths").TryGetProperty(Key(at), out var path) ? Cells(path) : null;
    public IReadOnlyList<BoardCell> Threat(int unit) => Cells(Data(unit).GetProperty("threat"));
    public IReadOnlyList<BoardCell> ControlZone(int unit) => Cells(Data(unit).GetProperty("control"));
    // The historical oracle supplies semantic cell clicks, not renderer coverage.
    public TacticalHit SourceHit(BoardCell at) => UnitAt(at) is { Visible: true } unit
        ? TacticalHit.Unit(at, unit.Id) : CanOpenRecruitment(at) ? TacticalHit.Object(at) : TacticalHit.Ground(at);
    public IReadOnlyList<TacticalCommand> CommandsAt(int unit, BoardCell at) => CommandsOverride ?? Data(unit)
        .GetProperty("commands").GetProperty(Key(at)).EnumerateArray().Select(option => new TacticalCommand(
            option.GetProperty("key").GetString()!, option.GetProperty("ability").GetString()!,
            option.TryGetProperty("weapon", out var weapon) ? weapon.GetString() : null,
            option.GetProperty("selfTargeted").GetBoolean(), option.GetProperty("hostile").GetBoolean(),
            Cells(option.GetProperty("targets")).Select(target => UnitAt(target) is { } subject
                ? TacticalTarget.Unit(target, subject.Id) : TacticalTarget.AtCell(target)).ToArray())).ToArray();
    public TacticalAttack Attack(int unit, BoardCell target, BoardCell from, string weapon) => AttackOverride ?? JsonSerializer.Deserialize<TacticalAttack>(Data(unit).GetProperty("attacks").GetProperty($"{Key(from)}:{Key(target)}:{weapon}"), JsonOptions)!;
    public IReadOnlyList<int> DeploymentUnits() => DeploymentOverride ?? Actors();
    public IReadOnlyList<BoardCell> DeploymentZone() => new[] { new BoardCell(0, 0), new BoardCell(1, 0) };
    public IReadOnlyList<TacticalTarget> DeploymentTargets(int unit) => DeploymentZone()
        .Select(at => UnitAt(at) is { } occupant && occupant.Id != unit
            ? TacticalTarget.Unit(at, occupant.Id) : TacticalTarget.AtCell(at)).ToArray();
    public IReadOnlyList<TacticalTactic> Tactics() => TacticsOverride;
    public IReadOnlyList<TacticalPassenger> Passengers(int carrier) => PassengersOverride;
    public IReadOnlyList<BoardCell> DirectionSteps(BoardCell from) => StepsOverride ?? Cells(Root.GetProperty("steps").GetProperty(Key(from)));
}
