#nullable enable
using System;
using System.Text.Json;

namespace Empire.Presentation;

/// <summary>Source presentation focus decisions over detached facts and injected cell geometry.</summary>
public static class BattlefieldFocus
{
    public static CameraPoint Initial(JsonElement state, Func<CameraPoint, CameraPoint> pointAt, CameraPoint fallback)
    {
        int? human = null;
        foreach (var player in state.GetProperty("players").EnumerateArray())
            if (player.GetProperty("controller").GetString() == "human")
            {
                human = player.GetProperty("id").GetInt32();
                break;
            }
        double x = 0, y = 0;
        var count = 0;
        foreach (var unit in state.GetProperty("units").EnumerateArray())
        {
            if (unit.GetProperty("owner").GetInt32() != human || unit.GetProperty("hp").GetDouble() <= 0) continue;
            var point = pointAt(Coordinates(unit));
            x += point.X;
            y += point.Y;
            count++;
        }
        return count == 0 ? fallback : new CameraPoint(x / count, y / count);
    }

    public static CameraPoint? CommandDestination(BattlePresentationTransition transition,
        Func<CameraPoint, CameraPoint> pointAt)
    {
        var action = transition.Action;
        if (!transition.IsComputerAction || action.GetProperty("kind").GetString() != "command"
            || !action.TryGetProperty("path", out var path) || path.GetArrayLength() < 2) return null;
        var id = action.GetProperty("unit").GetInt32();
        if (transition.BeforeVisibleUnitIds is not null && !transition.BeforeVisibleUnitIds.Contains(id)) return null;
        foreach (var unit in transition.Before.GetProperty("units").EnumerateArray())
            if (unit.GetProperty("id").GetInt32() == id) return pointAt(Coordinates(path[path.GetArrayLength() - 1]));
        return null;
    }

    private static CameraPoint Coordinates(JsonElement point) =>
        new(point.GetProperty("x").GetDouble(), point.GetProperty("y").GetDouble());
}
