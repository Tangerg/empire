#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    private sealed class IdleActor(Sprite2D sprite, ActorClip clip, float[] depths)
    {
        public Sprite2D Sprite { get; } = sprite;
        public ActorClip Clip { get; } = clip;
        public float[] Depths { get; } = depths;
        public double PhaseMs
        {
            get; set;
        }
        public int Frame { get; set; } = clip.FrameAt(0);
    }

    // Membership is pose ownership: finite actions remove their actor until completion.
    private Dictionary<Node2D, IdleActor> _idleActors = new();
    private bool _animationsEnabled = true;

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (_animationsEnabled == value)
                return;
            _animationsEnabled = value;
            if (!value)
            {
                var changed = false;
                foreach (var idle in _idleActors.Values)
                {
                    idle.PhaseMs = 0;
                    changed |= ApplyIdleFrame(idle, idle.Clip.FrameAt(0));
                }
                FinishIdleFrameChanges(changed);
            }
        }
    }

    private IdleActor CreateIdleActor(StandingEntry entry)
    {
        var actor = entry.Node;
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var clip = _assets!.ResolveUnitClip(actor.GetMeta("unit_type").AsString(), "idle", ActorClipUnit.Milliseconds,
            actor.HasMeta("actor_key") ? actor.GetMeta("actor_key").AsString() : null);
        var idle = new IdleActor(sprite, clip, ActorClipDepths(entry, sprite, clip));
        SetFrame(sprite, clip.Asset, idle.Frame);
        return idle;
    }

    private float[] ActorClipDepths(StandingEntry entry, Sprite2D sprite, ActorClip clip)
    {
        var depths = new HashSet<float> { StandingDepth(entry) };
        foreach (var frame in clip.FrameIndices.Distinct())
        {
            _assets!.ApplyFrame(sprite, clip.Asset, frame);
            depths.Add(StandingDepth(entry));
        }
        return depths.ToArray();
    }

    private void PrepareStandingPartitions(IEnumerable<StandingEntry> entries,
        IEnumerable<IdleActor>? idleActors = null, bool includeFinite = true) => Projection.PrepareDepthPartitions(
            entries.Where(entry => entry.Layer != 0).Select(StandingDepth),
            (idleActors ?? _idleActors.Values).SelectMany(idle => idle.Depths).Concat(
                includeFinite && _playback is { } playback ? playback.ActorDepths.Values.SelectMany(depths => depths) : Array.Empty<float>()));

    private void SuspendIdle(Node2D actor)
    {
        if (!_idleActors.Remove(actor))
            return;
        var asset = UnitAsset(actor);
        SetFrame(actor.GetNode<Sprite2D>("AuthoredSprite"), asset, AssetCatalog.FrameIndex(asset, "idle"));
        SortAnimatedStanding();
    }

    private void ResumeIdle(Node2D actor)
    {
        _playback?.ActorDepths.Remove(actor);
        var asset = UnitAsset(actor);
        SetFrame(actor.GetNode<Sprite2D>("AuthoredSprite"), asset, AssetCatalog.FrameIndex(asset, "idle"));
        var entries = CurrentStanding().ToArray();
        var entry = entries.Single(value => value.Node == actor);
        _idleActors.Add(actor, CreateIdleActor(entry));
        PrepareStandingPartitions(entries);
        SortAnimatedStanding();
    }

    private void AdvanceIdle(double milliseconds)
    {
        if (!AnimationsEnabled || _exiting || !IsInsideTree() || !IsVisibleInTree())
            return;
        var changed = false;
        foreach (var idle in _idleActors.Values)
        {
            var step = milliseconds % idle.Clip.Length;
            var remaining = idle.Clip.Length - idle.PhaseMs;
            idle.PhaseMs = (step >= remaining ? step - remaining : idle.PhaseMs + step) % idle.Clip.Length;
            changed |= ApplyIdleFrame(idle, idle.Clip.FrameAt(idle.PhaseMs));
        }
        FinishIdleFrameChanges(changed);
    }

    private bool ApplyIdleFrame(IdleActor idle, int frame)
    {
        if (idle.Frame == frame)
            return false;
        SetFrame(idle.Sprite, idle.Clip.Asset, frame);
        idle.Frame = frame;
        return true;
    }

    private void FinishIdleFrameChanges(bool changed)
    {
        if (!changed)
            return;
        SortAnimatedStanding();
        if (!IsPresenting)
            RefreshOcclusionCues();
    }
}
