#nullable enable
using Godot;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

public partial class BattleScreen
{
    private TacticalHit? PickPresentedField(CameraPoint point)
    {
        var size = _miniature is null ? _board.FieldSize : _mapArea.Size;
        if (point.X < 0 || point.Y < 0 || point.X >= size.X || point.Y >= size.Y)
            return null;
        var position = new Vector2((float)point.X, (float)point.Y);
        return _miniature is null ? _board.Pick(position) : _miniature.Pick(position);
    }

    private void RevealMiniatureCells(IEnumerable<BoardCell> cells, bool center = false)
    {
        if (_miniature is null || _camera is null || _gestures?.IsDragging == true)
            return;
        var contacts = cells.Select(cell => _miniature.Geometry.Contact(cell)).ToArray();
        _miniature.RevealContacts(_camera, contacts, _mapArea.Size, center);
        ApplyCamera();
    }

    private void RevealMiniatureTransition(BattlePresentationTransition transition)
    {
        CameraPoint Identity(CameraPoint point) => point;
        BoardCell Cell(CameraPoint point) => new((int)point.X, (int)point.Y);
        var attack = BattlefieldFocus.StationaryAttack(transition, Identity)
            ?? BattlefieldFocus.PlayerAttack(transition, Identity);
        if (attack is { } pair)
            RevealMiniatureCells(new[] { Cell(pair.Source), Cell(pair.Target) });
        else if (BattlefieldFocus.CommandDestination(transition, Identity) is { } destination)
            RevealMiniatureCells(new[] { Cell(destination) });
    }

    internal static bool HumanHandback(JsonElement before, JsonElement after)
    {
        if (after.GetProperty("phase").GetString() == "over")
            return false;
        var previous = before.GetProperty("currentPlayer").GetInt32();
        var current = after.GetProperty("currentPlayer").GetInt32();
        return previous != current && after.GetProperty("players").EnumerateArray()
            .Any(player => player.GetProperty("id").GetInt32() == current
                && player.GetProperty("controller").GetString() == "human")
            && before.GetProperty("players").EnumerateArray()
                .Any(player => player.GetProperty("id").GetInt32() == previous
                    && player.GetProperty("controller").GetString() != "human");
    }

    private void RevealMiniatureHandback(JsonElement state)
    {
        var owner = state.GetProperty("currentPlayer").GetInt32();
        RevealMiniatureCells(state.GetProperty("units").EnumerateArray()
            .Where(unit => unit.GetProperty("owner").GetInt32() == owner && unit.GetProperty("hp").GetDouble() > 0)
            .Select(unit => new BoardCell(unit.GetProperty("x").GetInt32(), unit.GetProperty("y").GetInt32())), true);
    }
}
