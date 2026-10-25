#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    private const float CueRadius = 10;
    private readonly List<(Polygon2D Marker, Coord Cell, int UnitId)> _unitCues = new();
    private Node2D? _cueLayer;

    private IReadOnlyList<string> RebuildOcclusionCues(IReadOnlyDictionary<int, Color> colors)
    {
        var diagnostics = new List<string>();
        _unitCues.Clear();
        _cueLayer = new Node2D { Name = "OccludedUnitCues", ZAsRelative = false, ZIndex = 2047, Visible = !IsPresenting };
        _projection!.AddChild(_cueLayer);
        var materials = _standing.Where(entry => entry.Visual is { } visual && StandingVisual.IsVisibleWithin(visual.Node, this) && visual.Node.Modulate.A > 0)
            .Select(entry => (Entry: entry, Visual: entry.Visual!, Bounds: entry.Visual!.GlobalBounds)).ToArray();
        var frames = _scenery!.GetNode<Node2D>("AuthoredFrame").GetChildren().OfType<Sprite2D>()
            .Where(sprite => StandingVisual.IsVisibleWithin(sprite, this) && sprite.Modulate.A > 0)
            .Select(sprite => (Visual: (StandingVisual)new SpriteStandingVisual(sprite), Bounds: new SpriteStandingVisual(sprite).GlobalBounds)).ToArray();
        var blockers = materials.Select(item => (item.Visual, item.Bounds)).Concat(frames).ToArray();
        var surfaces = Projection.Faces.Select(face => new[] { face.ProjectedA, face.ProjectedB, face.ProjectedC, face.ProjectedD }.Select(ToGlobal).ToArray())
            .Concat(_standing.Where(entry => entry.Surface is not null).SelectMany(entry => entry.Surface!.PointerPolygons(this)))
            .Select(polygon => (Polygon: polygon, Bounds: StandingVisual.BoundsOf(polygon))).ToArray();
        foreach (var actor in _standing.Where(entry => entry.UnitId is not null).OrderBy(entry => entry.UnitId))
        {
            var sprite = actor.Node.GetNode<Sprite2D>("AuthoredSprite");
            var actorBounds = actor.Visual!.GlobalBounds;
            var foreground = materials.Where(item => item.Entry.UnitId is null && item.Bounds.Intersects(actorBounds)
                && CompareDrawOrder(item.Entry.Node, item.Entry.Node.ZIndex, actor.Node, actor.Node.ZIndex) < 0).ToArray();
            if (foreground.Length == 0)
                continue;
            var rect = sprite.GetRect();
            var step = Math.Max(1, Math.Min(rect.Size.X, rect.Size.Y) / 16);
            var sampled = 0;
            var obscured = 0;
            for (var y = rect.Position.Y + step / 2; y < rect.End.Y; y += step)
                for (var x = rect.Position.X + step / 2; x < rect.End.X; x += step)
                {
                    var pixel = new Vector2(x, y);
                    if (!sprite.IsPixelOpaque(pixel))
                        continue;
                    sampled++;
                    var global = sprite.ToGlobal(pixel);
                    if (frames.Any(frame => frame.Bounds.HasPoint(global) && frame.Visual.Covers(global)))
                        continue;
                    if (foreground.Any(item => item.Bounds.HasPoint(global) && item.Visual.Covers(global) && !item.Visual.ContainsPointer(global)))
                        obscured++;
                }
            // A cue identifies a mostly covered, already render-visible actor; it never reveals a fog-filtered actor.
            if (sampled == 0 || obscured * 2 < sampled)
                continue;
            var head = ToLocal(sprite.ToGlobal(new(rect.GetCenter().X, rect.Position.Y))) + new Vector2(0, -CueRadius - 4);
            var candidates = from row in new[] { 0, -1, 1, -2, 2, -3 }
                             from column in new[] { 0, -1, 1, -2, 2, -3, 3 }
                             select head + new Vector2(column * 24, row * 24);
            var position = candidates.OrderBy(point => point.DistanceSquaredTo(head)).Where(point => CueFits(point, blockers, surfaces)).Select(point => (Vector2?)point).FirstOrDefault();
            if (position is not { } center)
            {
                diagnostics.Add($"No unobstructed occlusion cue position for unit {actor.UnitId}.");
                continue;
            }
            var marker = new Polygon2D
            {
                Name = $"UnitCue_{actor.UnitId}",
                Position = center,
                Polygon = Enumerable.Range(0, 24).Select(index => Vector2.FromAngle(index * Mathf.Tau / 24) * CueRadius).ToArray(),
                Color = colors[actor.Node.GetMeta("owner").AsInt32()]
            };
            var leader = new Line2D
            {
                Name = $"UnitCueLeader_{actor.UnitId}",
                Points = [actor.Node.Position, center],
                Width = 1,
                DefaultColor = new Color(0.94f, 0.9f, 0.76f, 0.7f),
                Antialiased = true
            };
            _cueLayer.AddChild(leader);
            _cueLayer.AddChild(marker);
            marker.AddChild(new Line2D
            {
                Points = Enumerable.Range(0, 25).Select(index => Vector2.FromAngle(index * Mathf.Tau / 24) * (CueRadius - .75f)).ToArray(),
                Width = 1.5f,
                DefaultColor = new Color(0.98f, 0.94f, 0.8f),
                Antialiased = true
            });
            marker.AddChild(new Polygon2D
            {
                Position = new(0, -3),
                Color = Colors.White,
                Polygon = Enumerable.Range(0, 16).Select(index => Vector2.FromAngle(index * Mathf.Tau / 16) * 2.5f).ToArray()
            });
            marker.AddChild(new Polygon2D { Color = Colors.White, Polygon = [new(-3.5f, 4), new(-2.5f, .5f), new(2.5f, .5f), new(3.5f, 4)] });
            _unitCues.Add((marker, actor.SelectionCell!.Value, actor.UnitId!.Value));
        }
        return diagnostics;
    }

    private bool CueFits(Vector2 center, IReadOnlyList<(StandingVisual Visual, Rect2 Bounds)> blockers,
        IReadOnlyList<(Vector2[] Polygon, Rect2 Bounds)> surfaces)
    {
        if (center.X < CueRadius || center.Y < CueRadius || center.X > FieldSize.X - CueRadius || center.Y > FieldSize.Y - CueRadius
            || _unitCues.Any(cue => cue.Marker.Position.DistanceTo(center) < 2 * CueRadius + 4))
            return false;
        // Test the actual filled polygon against pointer material, rather than sampling a grid that can miss thin posts.
        var polygon = Enumerable.Range(0, 24).Select(index => ToGlobal(center + Vector2.FromAngle(index * Mathf.Tau / 24) * CueRadius)).ToArray();
        var bounds = StandingVisual.BoundsOf(polygon);
        if (blockers.Any(blocker => blocker.Bounds.Intersects(bounds) && blocker.Visual.IntersectsPointer(polygon))
            || surfaces.Any(surface => surface.Bounds.Intersects(bounds) && Geometry2D.IntersectPolygons(surface.Polygon, polygon).Count > 0))
            return false;
        return true;
    }

    private bool FrameCovers(Vector2 global) => _scenery!.GetNode<Node2D>("AuthoredFrame").GetChildren().OfType<Sprite2D>()
        .Any(sprite => sprite.IsVisibleInTree() && sprite.Modulate.A > 0 && sprite.IsPixelOpaque(sprite.ToLocal(global)));

    private bool CueBlocked(Vector2 global) => FrameCovers(global) || Projection.Pick(ToLocal(global)) is { Cell: null }
        || _standing.Any(entry => entry.Surface?.HitPolygon(global) is not null) || _standing.Any(entry => entry.UnitId is null
        && entry.Visual is { } material && material.Node.IsVisibleInTree() && material.Node.Modulate.A > 0
        && material.Covers(global) && material.ContainsPointer(global));

    private TacticalHit? PickCue(Vector2 fieldPoint)
    {
        if (_unitCues.Count == 0 || IsPresenting || _cueLayer is null || !_cueLayer.IsVisibleInTree())
            return null;
        var global = ToGlobal(fieldPoint);
        foreach (var (marker, cell, id) in _unitCues)
            if (marker.IsVisibleInTree() && Geometry2D.IsPointInPolygon(marker.ToLocal(global), marker.Polygon))
                return CueBlocked(global) ? null : TacticalHit.Unit(new(cell.X, cell.Y), id);
        return null;
    }
}
