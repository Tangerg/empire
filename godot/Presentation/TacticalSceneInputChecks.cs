using System;
using Godot;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

internal static class TacticalSceneInputChecks
{
    internal static BoardCell Move(BattleScreen screen, ITacticalBattleSession session, int unitId = 2)
    {
        var queries = session.GetTacticalQueries(0);
        var origin = queries.Unit(unitId)?.At ?? throw new InvalidOperationException("Movement fixture has no actor.");
        var controller = new TacticalSelectionController(queries);
        foreach (var destination in queries.MoveStops(unitId))
        {
            if (destination == origin)
                continue;
            controller.Bind(queries);
            controller.Click(controller.Stamp, TacticalHit.Unit(origin, unitId));
            controller.Click(controller.Stamp, TacticalHit.Ground(destination));
            var result = controller.ChooseCommand(controller.Stamp, "wait");
            if (result.Request?.Action is not { } action || action.GetProperty("kind").GetString() != "command"
                || action.GetProperty("command").GetProperty("ability").GetString() != "wait")
                continue;
            screen.SelectHit(TacticalHit.Unit(origin, unitId));
            screen.SelectHit(TacticalHit.Ground(destination));
            screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.W, Pressed = true });
            return destination;
        }
        throw new InvalidOperationException("Movement fixture has no legal wait destination.");
    }
}
