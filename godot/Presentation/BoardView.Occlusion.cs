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
    // A settled projection owns stationary geometry; idle frame selection is its only changing shape input.
    // Cache values, never nodes, and retire them with the projection or its transform.
    private const int OcclusionPoseCapacity = 128;
    private sealed record OcclusionCue(int UnitId, Coord Cell, int Owner, Vector2 Contact, Vector2 Center);
    private sealed record OcclusionLayout(OcclusionCue[] Cues, string[] Diagnostics);
    private readonly Dictionary<string, OcclusionLayout> _occlusionPoses = new(StringComparer.Ordinal);
    private readonly Queue<string> _occlusionPoseOrder = new();
    private Transform2D? _occlusionPoseTransform;
    private readonly List<(Polygon2D Marker, Coord Cell, int UnitId)> _unitCues = new();
    private Node2D? _cueLayer;
    private IReadOnlyDictionary<int, Color>? _occlusionColors;
    private IReadOnlyList<string> _projectionDiagnostics = Array.Empty<string>();

    private void ClearOcclusionPoses()
    {
        _occlusionPoses.Clear();
        _occlusionPoseOrder.Clear();
        _occlusionPoseTransform = null;
    }

    internal void RefreshOcclusionCues()
    {
        ClearOcclusionPoses();
        RefreshIdleOcclusionCues();
    }

    private void RefreshIdleOcclusionCues()
    {
        if (_projection is null || _occlusionColors is null)
            return;
        if (IsPresenting || _occlusionPoseTransform != GlobalTransform)
            ClearOcclusionPoses();
        _occlusionPoseTransform = GlobalTransform;
        var pose = string.Join(";", _idleActors.OrderBy(pair => pair.Key.GetInstanceId())
            .Select(pair => $"{pair.Key.GetInstanceId()}:{pair.Value.Frame}"));
        if (!_occlusionPoses.TryGetValue(pose, out var layout))
        {
            layout = CalculateOcclusionLayout();
            if (_occlusionPoses.Count == OcclusionPoseCapacity)
                _occlusionPoses.Remove(_occlusionPoseOrder.Dequeue());
            _occlusionPoses.Add(pose, layout);
            _occlusionPoseOrder.Enqueue(pose);
        }
        if (_cueLayer is not null)
            RemoveEffect(_cueLayer);
        RenderOcclusionLayout(layout, _occlusionColors);
        _diagnostics = _projectionDiagnostics.Concat(layout.Diagnostics)
            .Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
    }

    private OcclusionLayout CalculateOcclusionLayout()
    {
        var diagnostics = new List<string>();
        var cues = new List<OcclusionCue>();
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
            var top = float.PositiveInfinity;
            for (var y = rect.Position.Y + step / 2; y < rect.End.Y; y += step)
                for (var x = rect.Position.X + step / 2; x < rect.End.X; x += step)
                {
                    var pixel = new Vector2(x, y);
                    if (!sprite.IsPixelOpaque(pixel))
                        continue;
                    sampled++;
                    var global = sprite.ToGlobal(pixel);
                    top = Math.Min(top, ToLocal(global).Y);
                    if (frames.Any(frame => frame.Bounds.HasPoint(global) && frame.Visual.Covers(global)))
                        continue;
                    if (foreground.Any(item => item.Bounds.HasPoint(global) && item.Visual.Covers(global)))
                        obscured++;
                }
            // A cue identifies a mostly covered, already render-visible actor; it never reveals a fog-filtered actor.
            if (sampled == 0 || obscured * 2 < sampled)
                continue;
            var head = new Vector2(actor.Node.Position.X, top - CueRadius - 4);
            const int spacing = 24;
            var firstColumn = (int)Math.Ceiling((CueRadius - head.X) / spacing);
            var lastColumn = (int)Math.Floor((FieldSize.X - CueRadius - head.X) / spacing);
            var firstRow = (int)Math.Ceiling((CueRadius - head.Y) / spacing);
            var lastRow = (int)Math.Floor((FieldSize.Y - CueRadius - head.Y) / spacing);
            var candidates = from row in Enumerable.Range(firstRow, Math.Max(0, lastRow - firstRow + 1))
                             from column in Enumerable.Range(firstColumn, Math.Max(0, lastColumn - firstColumn + 1))
                             select head + new Vector2(column * spacing, row * spacing);
            var position = candidates.OrderBy(point => point.DistanceSquaredTo(head)).Where(point => CueFits(point, blockers, surfaces, cues)).Select(point => (Vector2?)point).FirstOrDefault();
            if (position is not { } center)
            {
                diagnostics.Add($"No unobstructed occlusion cue position for unit {actor.UnitId}.");
                continue;
            }
            cues.Add(new(actor.UnitId!.Value, actor.SelectionCell!.Value, actor.Node.GetMeta("owner").AsInt32(), actor.Node.Position, center));
        }
        return new(cues.ToArray(), diagnostics.ToArray());
    }

    private void RenderOcclusionLayout(OcclusionLayout layout, IReadOnlyDictionary<int, Color> colors)
    {
        _unitCues.Clear();
        _cueLayer = new Node2D { Name = "OccludedUnitCues", ZAsRelative = false, ZIndex = 2047, Visible = !IsPresenting };
        _projection!.AddChild(_cueLayer);
        foreach (var cue in layout.Cues)
        {
            var marker = new Polygon2D
            {
                Name = $"UnitCue_{cue.UnitId}",
                Position = cue.Center,
                Polygon = [new(-7, -9), new(7, -9), new(7, 5), new(0, 10), new(-7, 5)],
                Color = colors[cue.Owner]
            };
            var leader = new Line2D
            {
                Name = $"UnitCueLeader_{cue.UnitId}",
                Points = [cue.Contact, cue.Center],
                Width = 1,
                DefaultColor = new Color(0.94f, 0.9f, 0.76f, 0.7f),
                Antialiased = true
            };
            _cueLayer.AddChild(leader);
            _cueLayer.AddChild(marker);
            marker.AddChild(new Line2D
            {
                Points = marker.Polygon.Append(marker.Polygon[0]).ToArray(),
                Width = 1.1f,
                DefaultColor = new Color(0.88f, 0.82f, 0.64f),
                Antialiased = true
            });
            marker.AddChild(new Polygon2D
            {
                Position = new(0, -3),
                Color = Colors.White,
                Polygon = Enumerable.Range(0, 16).Select(index => Vector2.FromAngle(index * Mathf.Tau / 16) * 2.5f).ToArray()
            });
            marker.AddChild(new Polygon2D { Color = Colors.White, Polygon = [new(-3.5f, 4), new(-2.5f, .5f), new(2.5f, .5f), new(3.5f, 4)] });
            _unitCues.Add((marker, cue.Cell, cue.UnitId));
        }
    }

    private bool CueFits(Vector2 center, IReadOnlyList<(StandingVisual Visual, Rect2 Bounds)> blockers,
        IReadOnlyList<(Vector2[] Polygon, Rect2 Bounds)> surfaces, IReadOnlyList<OcclusionCue> cues)
    {
        if (center.X < CueRadius || center.Y < CueRadius || center.X > FieldSize.X - CueRadius || center.Y > FieldSize.Y - CueRadius
            || cues.Any(cue => cue.Center.DistanceTo(center) < 2 * CueRadius + 4))
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
