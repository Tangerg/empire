#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;

namespace Empire.Presentation.Tactical;

public enum TacticalSelectionKind
{
    Idle, Deploying, DeploymentUnit, Unit, Destination, Target, Tactic, Disembark, Recruit
}
public enum TacticalInputStatus
{
    Ready, Ignored, Busy, NotHuman, Terminal, StaleBinding, StaleRevision
}
public enum TacticalRequestKind
{
    Action, Undo
}
public sealed record TacticalRequest(TacticalStamp Stamp, TacticalRequestKind Kind, JsonElement? Action);
public sealed record TacticalInputResult(TacticalInputStatus Status, TacticalRequest? Request = null,
    bool PreventDefault = false, BoardCell? Reveal = null, bool Center = false);

/// <summary>Only composition facts, never a mutable rule snapshot.</summary>
public sealed record TacticalSelection
{
    public TacticalSelectionKind Kind
    {
        get; init;
    }
    public int? UnitId
    {
        get; init;
    }
    public BoardCell? Destination
    {
        get; init;
    }
    public IReadOnlyList<BoardCell> Path { get; init; } = Array.Empty<BoardCell>();
    public IReadOnlyList<BoardCell> Targets { get; init; } = Array.Empty<BoardCell>();
    public string? Ability
    {
        get; init;
    }
    public string? Weapon
    {
        get; init;
    }
    public bool Hostile
    {
        get; init;
    }
    public string? Commander
    {
        get; init;
    }
    public int? Passenger
    {
        get; init;
    }
}

public sealed record TacticalSelectionOverlay(BoardCell? Selected, BoardCell? Cursor,
    IReadOnlyList<BoardCell> Move, IReadOnlyList<BoardCell> Attack, IReadOnlyList<BoardCell> Helpful,
    IReadOnlyList<BoardCell> Controlled, IReadOnlyList<BoardCell> Path, BoardCell? HoverTarget, IReadOnlyList<BoardCell> Threat);

/// <summary>
/// Sole local interaction owner. Every callback carries its rendered stamp; the session must
/// revalidate every emitted action. CompleteDispatch is called after success OR legal refusal.
/// </summary>
public sealed class TacticalSelectionController
{
    private ITacticalSelectionQueries queries;
    private TacticalStamp stamp;
    public TacticalSelection Selection
    {
        get; private set;
    }
    public BoardCell? Cursor
    {
        get; private set;
    }
    public int? InspectId
    {
        get; private set;
    }
    public bool Busy
    {
        get; private set;
    }
    public TacticalStamp Stamp => stamp;
    public IReadOnlySet<BoardCell> InteractiveCells => queries.InteractiveCells;
    public BoardCell? HoverTarget => queries.Stamp == stamp && Cursor is { } at && Selection.Targets.Contains(at) ? at : null;
    public bool ShowTactics => !Busy && queries.Stamp == stamp && queries.HumanTurn && Selection.Kind == TacticalSelectionKind.Idle;

    public TacticalSelectionController(ITacticalSelectionQueries queries)
    {
        this.queries = queries;
        stamp = queries.Stamp;
        Selection = Resting();
    }

    public void Bind(ITacticalSelectionQueries next)
    {
        queries = next;
        stamp = next.Stamp;
        Busy = false;
        Cursor = null;
        InspectId = null;
        Selection = Resting();
    }

    public void SetBusy(bool busy) => Busy = busy;

    public TacticalInputResult CompleteDispatch(TacticalStamp dispatched, ITacticalSelectionQueries next)
    {
        if (dispatched.Binding != stamp.Binding || next.Stamp.Binding != stamp.Binding)
            return Result(TacticalInputStatus.StaleBinding);
        if (dispatched.Revision != stamp.Revision || next.Stamp.Revision < stamp.Revision)
            return Result(TacticalInputStatus.StaleRevision);
        queries = next;
        stamp = next.Stamp;
        Busy = false;
        Selection = Resting();
        if (Cursor is { } cursor && !InteractiveCells.Contains(cursor))
            Cursor = null;
        return Result();
    }

    private TacticalSelection Resting() => Pick(queries.Phase == "deployment" ? TacticalSelectionKind.Deploying : TacticalSelectionKind.Idle);
    private static TacticalSelection Pick(TacticalSelectionKind kind, int? unit = null) => new() { Kind = kind, UnitId = unit };
    private static IReadOnlyList<BoardCell> Copy(IEnumerable<BoardCell> cells) => Array.AsReadOnly(cells.ToArray());
    private static TacticalInputResult Result(TacticalInputStatus status = TacticalInputStatus.Ready) => new(status);
    private TacticalInputStatus Guard(TacticalStamp expected, bool turn = false)
    {
        if (expected.Binding != stamp.Binding || queries.Stamp.Binding != stamp.Binding)
            return TacticalInputStatus.StaleBinding;
        if (expected.Revision != stamp.Revision || queries.Stamp.Revision != stamp.Revision)
            return TacticalInputStatus.StaleRevision;
        if (Busy)
            return TacticalInputStatus.Busy;
        if (queries.Phase == "over")
            return TacticalInputStatus.Terminal;
        if (turn ? !queries.HumanTurn : !queries.HumanInput)
            return TacticalInputStatus.NotHuman;
        return TacticalInputStatus.Ready;
    }

    private TacticalInputResult Action(JsonObject action) => new(TacticalInputStatus.Ready,
        new TacticalRequest(stamp, TacticalRequestKind.Action, EcmaJson.ToElement(action)));
    private static JsonObject Cell(BoardCell at) => new() { ["x"] = at.X, ["y"] = at.Y };
    private TacticalInputResult Command(TacticalCommand command, int unit, IReadOnlyList<BoardCell> path, BoardCell? target = null)
    {
        var order = new JsonObject { ["ability"] = command.Ability };
        if (command.Weapon is not null)
            order["weapon"] = command.Weapon;
        if (target is { } at)
            order["target"] = Cell(at);
        return Action(new JsonObject { ["kind"] = "command", ["unit"] = unit, ["path"] = new JsonArray(path.Select(at => (JsonNode)Cell(at)).ToArray()), ["command"] = order });
    }

    public TacticalInputResult Hover(TacticalStamp expected, BoardCell? at)
    {
        if (expected.Binding != stamp.Binding || queries.Stamp.Binding != stamp.Binding)
            return Result(TacticalInputStatus.StaleBinding);
        if (expected.Revision != stamp.Revision || queries.Stamp != stamp)
            return Result(TacticalInputStatus.StaleRevision);
        if (at is { } candidate && !InteractiveCells.Contains(candidate))
            return Leave(expected);
        Cursor = at;
        var unit = at is { } cell ? queries.UnitAt(cell) : null;
        if (unit?.Visible == true)
            InspectId = unit.Id;
        else if (Selection.UnitId is null)
            InspectId = null;
        return Result();
    }

    public TacticalInputResult Leave(TacticalStamp expected)
    {
        if (expected.Binding != stamp.Binding || queries.Stamp.Binding != stamp.Binding)
            return Result(TacticalInputStatus.StaleBinding);
        if (expected.Revision != stamp.Revision || queries.Stamp != stamp)
            return Result(TacticalInputStatus.StaleRevision);
        Cursor = null;
        return Result();
    }

    public TacticalInputResult Back(TacticalStamp expected)
    {
        // Source Escape precedes the human-turn guard, including AI and deployment.
        if (expected.Binding != stamp.Binding || queries.Stamp.Binding != stamp.Binding)
            return Result(TacticalInputStatus.StaleBinding);
        if (expected.Revision != stamp.Revision || queries.Stamp != stamp)
            return Result(TacticalInputStatus.StaleRevision);
        if (Busy)
            return Result(TacticalInputStatus.Busy);
        Selection = Selection.Kind switch
        {
            TacticalSelectionKind.Target => Selection with { Kind = TacticalSelectionKind.Destination, Targets = Array.Empty<BoardCell>(), Ability = null, Weapon = null, Hostile = false },
            TacticalSelectionKind.Destination => Pick(TacticalSelectionKind.Unit, Selection.UnitId),
            TacticalSelectionKind.DeploymentUnit => Pick(TacticalSelectionKind.Deploying),
            _ => Pick(TacticalSelectionKind.Idle),
        };
        if (Selection.Kind == Resting().Kind)
            InspectId = null;
        return Result();
    }

    private TacticalSelection Fresh(BoardCell at)
    {
        var unit = queries.UnitAt(at);
        if (unit is not null)
            return queries.HumanTurn && unit.CanAct ? Pick(TacticalSelectionKind.Unit, unit.Id) : Pick(TacticalSelectionKind.Idle);
        return queries.HumanTurn && queries.CanOpenRecruitment(at)
            ? new TacticalSelection { Kind = TacticalSelectionKind.Recruit, Destination = at }
            : Pick(TacticalSelectionKind.Idle);
    }
    private TacticalSelection DeploymentAt(BoardCell at)
    {
        var unit = queries.UnitAt(at);
        return unit is not null && queries.DeploymentUnits().Contains(unit.Id)
            ? Pick(TacticalSelectionKind.DeploymentUnit, unit.Id) : Pick(TacticalSelectionKind.Deploying);
    }

    public TacticalInputResult Click(TacticalStamp expected, BoardCell at)
    {
        var guard = Guard(expected);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        if (!InteractiveCells.Contains(at))
            return Result(TacticalInputStatus.Ignored);
        var selected = Selection;
        var unit = selected.UnitId is { } id ? queries.Unit(id) : null;
        switch (selected.Kind)
        {
            case TacticalSelectionKind.Deploying:
                Selection = DeploymentAt(at);
                break;
            case TacticalSelectionKind.DeploymentUnit:
                if (unit is null)
                    Selection = Pick(TacticalSelectionKind.Deploying);
                else if (queries.DeploymentSpots(unit.Id).Contains(at))
                    return Action(new JsonObject { ["kind"] = "deployUnit", ["unit"] = unit.Id, ["at"] = Cell(at) });
                else
                    Selection = DeploymentAt(at);
                break;
            case TacticalSelectionKind.Target:
            case TacticalSelectionKind.Tactic:
            case TacticalSelectionKind.Disembark:
                if (!selected.Targets.Contains(at))
                {
                    Selection = Pick(TacticalSelectionKind.Idle);
                    break;
                }
                if (selected.Kind == TacticalSelectionKind.Tactic)
                    return Action(new JsonObject { ["kind"] = "tactic", ["commander"] = selected.Commander, ["tactic"] = selected.Ability, ["target"] = Cell(at) });
                if (selected.Kind == TacticalSelectionKind.Disembark)
                    return Action(new JsonObject { ["kind"] = "disembark", ["carrier"] = selected.UnitId, ["unit"] = selected.Passenger, ["at"] = Cell(at) });
                return Command(new TacticalCommand("", selected.Ability!, selected.Weapon, false, selected.Hostile, selected.Targets), selected.UnitId!.Value, selected.Path, at);
            case TacticalSelectionKind.Unit:
            case TacticalSelectionKind.Destination:
                if (unit is null || !queries.HumanTurn || !unit.CanAct)
                {
                    Selection = Fresh(at);
                    break;
                }
                var clicked = queries.UnitAt(at);
                if (clicked is { Visible: true } && queries.AreEnemies(unit.Id, clicked.Id) && ArmBestAttack(unit, clicked))
                    break;
                if (queries.MoveStops(unit.Id).Contains(at) && queries.PathTo(unit.Id, at) is { } path)
                {
                    var commands = queries.CommandsAt(unit.Id, at);
                    if (commands.Count == 1 && commands[0].SelfTargeted)
                        return Command(commands[0], unit.Id, path);
                    Selection = new TacticalSelection { Kind = TacticalSelectionKind.Destination, UnitId = unit.Id, Destination = at, Path = Copy(path) };
                }
                else
                    Selection = Fresh(at);
                break;
            default:
                Selection = Fresh(at);
                break;
        }
        var inspect = queries.UnitAt(at);
        InspectId = inspect?.Visible == true ? inspect.Id : null;
        return Result();
    }

    private bool ArmBestAttack(TacticalUnit unit, TacticalUnit target)
    {
        double? best = null;
        TacticalSelection? choice = null;
        foreach (var at in queries.MoveStops(unit.Id))
            foreach (var option in queries.CommandsAt(unit.Id, at))
            {
                if (option.Weapon is null || !option.Targets.Contains(target.At))
                    continue;
                var plan = queries.Attack(unit.Id, target.At, at, option.Weapon);
                if (!plan.HasPrimaryUnit)
                    continue;
                var score = plan.StrikeDamage * 2 + plan.SplashDamage * 1.25 + plan.TerrainDefense * 60 - plan.CounterDamage * 1.5 - (at != unit.At ? 1 : 0);
                if (best is not null && score <= best)
                    continue;
                best = score;
                choice = Target(unit.Id, at, queries.PathTo(unit.Id, at) ?? new[] { unit.At }, option);
            }
        if (choice is null)
            return false;
        Selection = choice;
        return true;
    }
    private static TacticalSelection Target(int unit, BoardCell at, IReadOnlyList<BoardCell> path, TacticalCommand option) => new()
    {
        Kind = TacticalSelectionKind.Target,
        UnitId = unit,
        Destination = at,
        Path = Copy(path),
        Ability = option.Ability,
        Weapon = option.Weapon,
        Hostile = option.Hostile,
        Targets = Copy(option.Targets),
    };

    public TacticalInputResult ChooseCommand(TacticalStamp expected, string keyOrAbility)
    {
        var guard = Guard(expected, true);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        var selected = Selection;
        if (selected.Kind != TacticalSelectionKind.Destination || selected.UnitId is not { } unit || selected.Destination is not { } at || queries.Unit(unit) is null)
            return Result(TacticalInputStatus.Ignored);
        var options = queries.CommandsAt(unit, at);
        var option = options.FirstOrDefault(value => value.Key == keyOrAbility) ?? options.FirstOrDefault(value => value.Ability == keyOrAbility);
        if (option is null)
            return Result(TacticalInputStatus.Ignored);
        if (option.SelfTargeted)
            return Command(option, unit, selected.Path);
        if (option.Targets.Count == 1)
            return Command(option, unit, selected.Path, option.Targets[0]);
        Selection = Target(unit, at, selected.Path, option);
        return Result();
    }

    public TacticalInputResult ChooseTactic(TacticalStamp expected, string key)
    {
        var guard = Guard(expected, true);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        var separator = key.IndexOf(':');
        if (separator < 1)
            return Result(TacticalInputStatus.Ignored);
        var option = queries.Tactics().FirstOrDefault(value => value.Commander == key[..separator] && value.Id == key[(separator + 1)..]);
        if (option is null)
            return Result(TacticalInputStatus.Ignored);
        if (option.Targets.Count == 1)
            return Action(new JsonObject { ["kind"] = "tactic", ["commander"] = option.Commander, ["tactic"] = option.Id, ["target"] = Cell(option.Targets[0]) });
        Selection = new TacticalSelection { Kind = TacticalSelectionKind.Tactic, Commander = option.Commander, Ability = option.Id, Targets = Copy(option.Targets) };
        return Result();
    }

    public TacticalInputResult PickDeploymentUnit(TacticalStamp expected, int id)
    {
        var guard = Guard(expected);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        if (queries.Phase != "deployment" || !queries.DeploymentUnits().Contains(id) || queries.Unit(id) is not { } unit)
            return Result(TacticalInputStatus.Ignored);
        Selection = Pick(TacticalSelectionKind.DeploymentUnit, id);
        InspectId = id;
        return new TacticalInputResult(TacticalInputStatus.Ready, Reveal: unit.At, Center: true);
    }

    public TacticalInputResult ChooseDisembark(TacticalStamp expected, int passenger)
    {
        var guard = Guard(expected, true);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        if (Selection.UnitId is not { } id || queries.Unit(id)?.CanAct != true)
            return Result(TacticalInputStatus.Ignored);
        var option = queries.Passengers(id).FirstOrDefault(value => value.Id == passenger);
        if (option is null || option.Spots.Count == 0)
            return Result(TacticalInputStatus.Ignored);
        Selection = new TacticalSelection { Kind = TacticalSelectionKind.Disembark, UnitId = id, Passenger = passenger, Targets = Copy(option.Spots) };
        return Result();
    }

    public TacticalInputResult Recruit(TacticalStamp expected, string type)
    {
        var guard = Guard(expected, true);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        return Selection.Kind == TacticalSelectionKind.Recruit && Selection.Destination is { } at
            ? Action(new JsonObject { ["kind"] = "recruit", ["at"] = Cell(at), ["unit"] = type }) : Result(TacticalInputStatus.Ignored);
    }

    private TacticalInputResult Cycle()
    {
        var actors = queries.Actors().Where(id => queries.Unit(id) is { } unit && InteractiveCells.Contains(unit.At)).ToArray();
        if (actors.Length == 0)
            return Result(TacticalInputStatus.Ignored);
        var current = Selection.Kind == TacticalSelectionKind.Unit ? Selection.UnitId : null;
        var index = -1;
        for (var i = 0; i < actors.Length; i++)
            if (actors[i] == current)
            {
                index = i;
                break;
            }
        var id = actors[(index + 1) % actors.Length];
        Selection = Pick(TacticalSelectionKind.Unit, id);
        InspectId = id;
        return new TacticalInputResult(TacticalInputStatus.Ready, Reveal: queries.Unit(id)?.At, Center: true);
    }

    public TacticalInputResult Key(TacticalStamp expected, string key)
    {
        var k = key.ToLowerInvariant();
        if (k == "escape")
            return Back(expected);
        var guard = Guard(expected, true);
        if (guard != TacticalInputStatus.Ready)
            return Result(guard);
        if (k == "e")
            return Action(new JsonObject { ["kind"] = "endTurn" });
        if (k == "u")
            return queries.CanUndo ? new TacticalInputResult(TacticalInputStatus.Ready, new TacticalRequest(stamp, TacticalRequestKind.Undo, null)) : Result(TacticalInputStatus.Ignored);
        if (k is " " or "tab")
            return Cycle() with
            {
                PreventDefault = true
            };
        var command = k switch
        {
            "a" => "attack",
            "c" => "capture",
            "h" => "heal",
            "w" => "wait",
            _ => null
        };
        if (command is not null && Selection.Kind == TacticalSelectionKind.Destination)
            return ChooseCommand(expected, command);
        var direction = k switch
        {
            "arrowup" or "w" => (0, -1),
            "arrowdown" or "s" => (0, 1),
            "arrowleft" or "a" => (-1, 0),
            "arrowright" or "d" => (1, 0),
            _ => (0, 0)
        };
        if (direction != (0, 0))
        {
            var from = Cursor ?? (Selection.UnitId is { } id ? queries.Unit(id)?.At : null) ?? (InspectId is { } inspect ? queries.Unit(inspect)?.At : null);
            if (from is null)
                return Cycle() with
                {
                    PreventDefault = true
                };
            BoardCell? best = null;
            var reach = 0;
            foreach (var to in queries.DirectionSteps(from.Value))
            {
                if (!InteractiveCells.Contains(to))
                    continue;
                var score = (to.X - from.Value.X) * direction.Item1 + (to.Y - from.Value.Y) * direction.Item2;
                if (score <= reach)
                    continue;
                best = to;
                reach = score;
            }
            if (best is { } target)
                Hover(expected, target);
            return new TacticalInputResult(TacticalInputStatus.Ready, PreventDefault: true, Reveal: best);
        }
        if (k == "enter")
            return (Cursor is { } at ? Click(expected, at) : Result(TacticalInputStatus.Ignored)) with
            {
                PreventDefault = true
            };
        return Result(TacticalInputStatus.Ignored);
    }

    public TacticalSelectionOverlay Overlay()
    {
        var move = new List<BoardCell>();
        var attack = new List<BoardCell>();
        var helpful = new List<BoardCell>();
        var controlled = new List<BoardCell>();
        var threat = new List<BoardCell>();
        IReadOnlyList<BoardCell> path = Array.Empty<BoardCell>();
        BoardCell? selected = null;
        if (!Busy && queries.Stamp == stamp)
        {
            var selection = Selection;
            var unit = selection.UnitId is { } id ? queries.Unit(id) : null;
            if (unit is not null)
                selected = unit.At;
            if (unit is null || queries.HumanTurn && unit.CanAct)
                switch (selection.Kind)
                {
                    case TacticalSelectionKind.Unit when unit is not null:
                        move.AddRange(queries.MoveStops(unit.Id));
                        attack.AddRange(queries.Threat(unit.Id).Where(at => !move.Contains(at)));
                        controlled.AddRange(queries.ControlZone(unit.Id));
                        if (Cursor is { } cursor && move.Contains(cursor))
                            path = queries.PathTo(unit.Id, cursor) ?? path;
                        break;
                    case TacticalSelectionKind.Destination:
                    case TacticalSelectionKind.Target:
                        selected = selection.Destination;
                        path = selection.Path;
                        if (selection.Kind == TacticalSelectionKind.Target)
                        {
                            (selection.Hostile ? attack : helpful).AddRange(selection.Targets);
                            if (selection.Weapon is { } weapon && HoverTarget is { } target && unit is not null)
                                attack.AddRange(queries.Attack(unit.Id, target, selection.Destination!.Value, weapon).AffectedCells);
                        }
                        break;
                    case TacticalSelectionKind.Deploying:
                        move.AddRange(queries.DeploymentZone());
                        break;
                    case TacticalSelectionKind.DeploymentUnit when unit is not null:
                        selected = unit.At;
                        move.AddRange(queries.DeploymentSpots(unit.Id));
                        break;
                    case TacticalSelectionKind.Disembark:
                        selected = unit?.At;
                        helpful.AddRange(selection.Targets);
                        break;
                    case TacticalSelectionKind.Tactic:
                        helpful.AddRange(selection.Targets);
                        break;
                }
            if (unit is null && InspectId is { } inspectedId && queries.Unit(inspectedId) is { Visible: true } inspected && queries.IsEnemyOfViewer(inspectedId))
            {
                threat.AddRange(queries.Threat(inspectedId));
                selected = inspected.At;
            }
        }
        return new TacticalSelectionOverlay(selected, Cursor, Copy(move.Distinct()), Copy(attack.Distinct()), Copy(helpful.Distinct()), Copy(controlled.Distinct()), Copy(path), Busy ? null : HoverTarget, Copy(threat.Distinct()));
    }
}
