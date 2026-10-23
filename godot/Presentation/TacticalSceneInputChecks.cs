using System;
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
            var result = controller.Click(controller.Stamp, TacticalHit.Ground(destination));
            if (result.Request?.Action is not { } action || action.GetProperty("kind").GetString() != "command"
                || action.GetProperty("command").GetProperty("ability").GetString() != "wait")
                continue;
            screen.SelectHit(TacticalHit.Unit(origin, unitId));
            screen.SelectHit(TacticalHit.Ground(destination));
            return destination;
        }
        throw new InvalidOperationException("Movement fixture has no automatic wait destination.");
    }
}
