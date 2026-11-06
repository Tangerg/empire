#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Lifecycle;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    private sealed record AttackPlayback(int Attacker, string Weapon, Task<bool> Recovery);
    private sealed record Playback(BattlePresentationTransition Transition, JsonObject After,
        IReadOnlySet<int>? AfterVisible, PresentationTimeline Clock, Node2D Effects, Control ViewportEffects)
    {
        internal AttackPlayback? Attack
        {
            get; set;
        }
        internal Dictionary<Node2D, float[]> ActorDepths { get; } = new();
    }
    private Playback? _playback;
    private bool _exiting;

    public bool IsPresenting => _playback is not null;
    internal bool IsExiting => _exiting;
    internal object? PresentationIdentity => _playback;
    public event Action? PresentationRepaintRequested;
    public event Action<BattlePresentationTransition, int>? EventPresented;
    internal bool AdvanceAnimationsAutomatically { get; set; } = true;

    public override void _EnterTree() => _exiting = false;

    public override void _Process(double delta)
    {
        if (AdvanceAnimationsAutomatically)
            AdvancePresentation(delta);
    }

    internal void AdvancePresentation(double seconds)
    {
        var milliseconds = seconds * 1000;
        if (!double.IsFinite(milliseconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        AdvanceIdle(milliseconds);
        _playback?.Clock.Advance(seconds);
    }

    public override void _ExitTree()
    {
        _exiting = true;
        _idleActors.Clear();
        CancelPresentation();
    }

    public void CancelPresentation()
    {
        var playback = _playback;
        if (playback is null)
            return;
        _playback = null;
        // Resolving a waiter may synchronously start another playback. Finish this
        // playback's cleanup and settlement before handing control back to it.
        try
        {
            RemoveEffect(playback.Effects);
            RemoveEffect(playback.ViewportEffects);
            if (!_exiting)
                RenderState(playback.After, playback.AfterVisible);
        }
        finally { playback.Clock.Cancel(); }
    }

    /// <summary>Plays committed facts; it cannot dispatch, forecast, or edit canonical state.</summary>
    public async Task<bool> PresentTransitionAsync(Control viewportFeedback, BattlePresentationTransition transition,
        IReadOnlySet<int>? afterVisibleUnitIds = null)
    {
        if (IsPresenting)
            throw new InvalidOperationException("A battle transition is already being presented.");
        if (_exiting || !IsInsideTree())
            return false;
        ArgumentNullException.ThrowIfNull(viewportFeedback);
        if (!GodotObject.IsInstanceValid(viewportFeedback) || !viewportFeedback.IsInsideTree())
            throw new InvalidOperationException("The viewport feedback host must be mounted for presentation.");
        var before = ReadState(transition.Before);
        var after = ReadState(transition.After);
        var afterVisible = afterVisibleUnitIds?.ToFrozenSet();
        var action = ReadState(transition.Action);
        var events = transition.Events.Select(ReadState).ToArray();
        RenderState(before, transition.BeforeVisibleUnitIds);
        var effects = new Node2D { Name = "BattleEffects", ZIndex = 2500 };
        AddChild(effects);
        var viewportEffects = new Control
        {
            Name = "ViewportEffects",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        };
        viewportFeedback.AddChild(viewportEffects);
        viewportEffects.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var playback = new Playback(transition, after, afterVisible,
            new PresentationTimeline { MotionEnabled = AnimationsEnabled }, effects, viewportEffects);
        _playback = playback;
        _cueLayer!.Visible = false;
        try
        {
            // Source game.ts marches the complete command path before its event
            // presenters, including before a parting shot. The move event is not
            // a second march. Only an accepted receipt reaches this adapter.
            if (Text(action, "kind") == "command" && action["path"] is JsonArray path && path.Count > 1)
                if (!await AnimatePath(playback, Integer(action, "unit"), path, true))
                    return false;
            for (var index = 0; index < events.Length; index++)
            {
                if (!ReferenceEquals(_playback, playback))
                    return false;
                if (!await PresentEvent(playback, events[index], transition.EventDescriptions[index]))
                    return false;
                if (!ReferenceEquals(_playback, playback))
                    return false;
                EventPresented?.Invoke(transition, index);
            }
            if (playback.Attack is not null)
                throw new InvalidOperationException("Main attack activation has no completion boundary.");
            return true;
        }
        finally
        {
            if (ReferenceEquals(_playback, playback))
            {
                _playback = null;
                playback.Clock.Cancel();
                RemoveEffect(effects);
                RemoveEffect(viewportEffects);
                if (!_exiting)
                    RenderState(after, afterVisible);
            }
        }
    }

    private async Task<bool> PresentEvent(Playback playback, JsonObject item, string description)
    {
        switch (Text(item, "type"))
        {
            case "attackActivated":
                return await BeginAttack(playback, item);
            case "attackCompleted":
                return await CompleteAttack(playback, item);
            case "attack":
            case "counter":
            case "supportAttack":
            case "partingShot":
            case "areaAttack":
                {
                    var defender = Integer(item, "defender");
                    if (!WasVisible(playback, defender))
                        return true;
                    var at = Text(item, "type") == "partingShot" ? CellCentre(Coordinate(item["at"]!)) : ImpactOn(defender);
                    return await PlayStrike(playback, item, at, Text(item, "type") is "counter" or "supportAttack" or "partingShot", "killed", DrawnUnit(defender));
                }
            case "attackStructure":
            case "areaAttackStructure":
                {
                    if (!WasVisible(playback, Integer(item, "attacker")))
                        return true;
                    var structure = Objects(playback.After, "structures").FirstOrDefault(value => Text(value, "id") == Text(item, "structure"));
                    return await PlayStrike(playback, item, structure is null ? null : CellCentre(Coordinate(structure)), false, "destroyed");
                }
            case "death":
                if (playback.Attack is { } dying && dying.Attacker == Integer(item, "unit") && !await dying.Recovery)
                    return false;
                return await AnimateDeath(playback, Integer(item, "unit"));
            case "forcedMove":
                {
                    var id = Integer(item, "unit");
                    if (playback.Attack is { } displaced && displaced.Attacker == id && !await displaced.Recovery)
                        return false;
                    if (!BattleEventVisibility.UnitWasAndIsVisible(id, playback.Transition.BeforeVisibleUnitIds, playback.AfterVisible)
                        || DrawnUnit(id) is not { } actor)
                        return true;
                    if (Text(item, "mode") == "teleport")
                    {
                        SuspendIdle(actor);
                        var to = Coordinate(item["to"]!);
                        var destination = LogicalContact(to.X, to.Y);
                        PrepareAnimatedPath(actor, new[] { destination });
                        PlaceAnimatedActor(actor, destination);
                        ResumeIdle(actor);
                        return true;
                    }
                    return await AnimatePath(playback, id, item["path"]!.AsArray(), false);
                }
            case "collisionDamage":
                {
                    var id = Integer(item, "unit");
                    return !BattleEventVisibility.UnitWasAndIsVisible(id, playback.Transition.BeforeVisibleUnitIds, playback.AfterVisible)
                        || ImpactOn(id) is not { } at
                        || await AnimateUnitDamage(playback, id, at, Number(item["amount"], "collision amount"), item["killed"]!.GetValue<bool>(), null);
                }
            case "statusTick":
                {
                    var id = Integer(item, "unit");
                    return !BattleEventVisibility.UnitWasAndIsVisible(id, playback.Transition.BeforeVisibleUnitIds, playback.AfterVisible)
                        || ImpactOn(id) is not { } at
                        || await AnimateUnitDamage(playback, id, at, Number(item["amount"], "status amount"), false, null);
                }
            case "heal":
                {
                    var target = Integer(item, "target");
                    var unit = FindUnit(playback.After, target);
                    if (unit is null || !WasVisible(playback, target) || ImpactOn(target) is not { } at)
                        return true;
                    var source = Integer(item, "source");
                    var amount = Number(item["amount"], "heal amount");
                    var topic = _assets!.ResolveHealEffect()?.TopicId;
                    return WasVisible(playback, source) && DrawnUnit(source) is { } actor
                        ? await AnimateActorAction(playback, actor, _assets.ResolveAbilityAction("heal"), at, amount, true, false, topic)
                        : await AnimateImpact(playback, at, amount, true, false, topic);
                }
            case "regen":
                {
                    var id = Integer(item, "unit");
                    var unit = FindUnit(playback.After, id);
                    return unit is null || ImpactOn(id) is not { } at
                        || !BattleEventVisibility.UnitWasAndIsVisible(id, playback.Transition.BeforeVisibleUnitIds, playback.AfterVisible)
                        || await AnimateImpact(playback, at, Number(item["amount"], "regen amount"), true, false, _assets!.ResolveHealEffect()?.TopicId);
                }
            case "recruit":
                // A HUD refresh cannot settle unrelated actors ahead of later events.
                PresentRecruit(playback, item);
                PresentationRepaintRequested?.Invoke();
                return await AnimateSpawn(playback, Integer(item, "unit"));
            case "capture":
                // Source capture refreshes map ownership, not the entire actor set.
                // The settled snapshot remains the sole owner of every captured cell.
                if (item["captured"]!.GetValue<bool>())
                    RefreshCapturedSites(playback);
                return true;
            case "turnStart":
                {
                    var player = Objects(playback.After, "players").FirstOrDefault(value => Integer(value, "id") == Integer(item, "player"));
                    if (player is null)
                        return true;
                    PresentationRepaintRequested?.Invoke();
                    return await Announce(playback, description, Color.FromHtml(Text(player, "color")));
                }
            case "gameOver":
                return await AnimateCelebration(playback, BattleConclusion.Parse(item["conclusion"]));
            default:
                // The source registry deliberately leaves these events without an
                // animation, including casts and non-death departures.
                return true;
        }
    }

    private async Task<bool> BeginAttack(Playback playback, JsonObject item)
    {
        if (playback.Attack is not null)
            throw new InvalidOperationException("Main attack activations cannot overlap.");
        var id = Integer(item, "attacker");
        var weapon = Text(item, "weapon");
        var recovery = Task.FromResult(true);
        var release = 0.0;
        if (WasVisible(playback, id) && DrawnUnit(id) is { } actor)
        {
            var aim = Coordinate(item["at"]!);
            var visual = PrepareActorAction(actor, _assets!.ResolveWeaponAction(weapon), Projection.Project(LogicalContact(aim.X, aim.Y)));
            release = visual.Clip.RequireRelease();
            visual.Sample(0);
            recovery = playback.Clock.TweenAsync(visual.Clip.Length, t => visual.Sample(t * visual.Clip.Length));
        }
        playback.Attack = new(id, weapon, recovery);
        if (!await playback.Clock.WaitAsync(release))
            return false;
        if (recovery.IsFaulted)
            await recovery;
        return true;
    }

    private static async Task<bool> CompleteAttack(Playback playback, JsonObject item)
    {
        if (playback.Attack is not { } attack || attack.Attacker != Integer(item, "attacker") || attack.Weapon != Text(item, "weapon"))
            throw new InvalidOperationException("Main attack completion does not match its activation.");
        var complete = await attack.Recovery;
        playback.Attack = null;
        return complete;
    }

    private async Task<bool> PlayStrike(Playback playback, JsonObject item, Vector2? at, bool windup, string fatalField, Node2D? recipient = null)
    {
        var attacker = Integer(item, "attacker");
        var weapon = Text(item, "weapon");
        var amount = Number(item["damage"], "damage");
        var fatal = item[fatalField]!.GetValue<bool>();
        var topic = at is null ? null : _assets!.ResolveWeaponEffect(weapon)?.TopicId;
        if (windup && WasVisible(playback, attacker) && DrawnUnit(attacker) is { } actor)
            return await AnimateActorAction(playback, actor, _assets!.ResolveWeaponAction(weapon), at, amount, false, fatal, topic, recipient);
        if (recipient is not null && playback.Attack is { } active && DrawnUnit(active.Attacker) == recipient && !await active.Recovery)
            return false;
        return at is null || await AnimateImpact(playback, at.Value, amount, false, fatal, topic, recipient);
    }

    private async Task<bool> AnimateUnitDamage(Playback playback, int id, Vector2 at, double amount, bool fatal, string? topic)
    {
        if (playback.Attack is { } active && active.Attacker == id && !await active.Recovery)
            return false;
        return await AnimateImpact(playback, at, amount, false, fatal, topic, DrawnUnit(id));
    }

    private async Task<bool> AnimatePath(Playback playback, int id, JsonArray path, bool walking)
    {
        var actor = DrawnUnit(id);
        if (actor is null || path.Count < 2 || !WasVisible(playback, id))
            return true;
        SuspendIdle(actor);
        var asset = UnitAsset(actor);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var clip = walking ? _assets!.ResolveUnitClip(actor.GetMeta("unit_type").AsString(), "move", ActorClipUnit.Cells,
            actor.HasMeta("actor_key") ? actor.GetMeta("actor_key").AsString() : null) : null;
        // Presentation pace only: one duration per travelled cell. The committed
        // receipt still owns the path, destination and event order. A single tween
        // preserves progress across cell boundaries instead of losing frame overshoot.
        var duration = walking ? playback.Transition.IsComputerAction ? 110.0 : 160.0 : 120.0;
        var contacts = path.Select(point => Coordinate(point!))
            .Select(point => LogicalContact(point.X, point.Y)).ToArray();
        PrepareAnimatedPath(actor, contacts, clip);
        var steps = contacts.Length - 1;
        var frame = clip?.FrameAt(0) ?? AssetCatalog.FrameIndex(asset, "idle");
        SetFrame(sprite, clip?.Asset ?? asset, frame);
        SortAnimatedStanding();
        if (!await playback.Clock.TweenAsync(duration * steps, t =>
        {
            var distance = t * steps;
            var segment = Math.Min((int)Math.Floor(distance), steps - 1);
            var fraction = (float)(distance - segment);
            var start = contacts[segment];
            var end = contacts[segment + 1];
            var visualDirection = Projection.Project(end).X - Projection.Project(start).X;
            var facingChanged = visualDirection != 0 && (sprite.Scale.X < 0) != (visualDirection < 0);
            if (visualDirection != 0)
                Face(sprite, visualDirection < 0);
            var logical = start.Lerp(end, fraction);
            // Keep the actor's support plane and its composited contact shadow on
            // the road. The drawn walk poses provide the gait, not a whole-body hop.
            var nextFrame = clip?.FrameAt(distance % clip.Length) ?? frame;
            if (nextFrame != frame || facingChanged)
            {
                SetFrame(sprite, clip?.Asset ?? asset, nextFrame);
                frame = nextFrame;
            }
            PlaceAnimatedActor(actor, logical);
        }))
            return false;
        ResumeIdle(actor);
        return true;
    }

    private void PlaceAnimatedActor(Node2D actor, Vector2 logical)
    {
        actor.SetMeta("logical_contact", logical);
        actor.Position = Projection.Project(logical);
        SortAnimatedStanding();
    }

    private void PrepareAnimatedPath(Node2D actor, IReadOnlyList<Vector2> contacts, ActorClip? clip = null)
    {
        var entries = CurrentStanding().ToArray();
        var movingEntry = entries.Single(entry => entry.Node == actor);
        var originDepth = Projection.Depth(movingEntry.Contact);
        var poseDepths = clip is null ? new[] { StandingDepth(movingEntry) }
            : ActorClipDepths(movingEntry, actor.GetNode<Sprite2D>("AuthoredSprite"), clip);
        var offsets = poseDepths.Select(depth => depth - originDepth).ToArray();
        var depths = contacts.SelectMany(contact => offsets.Select(offset => Projection.Depth(contact) + offset)).ToArray();
        var first = (int)Math.Floor(depths.Min());
        var last = (int)Math.Ceiling(depths.Max());
        PrepareFiniteActorDepths(actor, Enumerable.Range(first, last - first + 1).Select(depth => (float)depth));
    }

    private sealed record ActorActionVisual(ActorClip Clip, Action<double> Sample);

    private ActorActionVisual PrepareActorPose(Node2D actor, string action, bool? faceLeft = null)
    {
        var clip = _assets!.ResolveUnitClip(actor.GetMeta("unit_type").AsString(), action, ActorClipUnit.Milliseconds,
            actor.HasMeta("actor_key") ? actor.GetMeta("actor_key").AsString() : null);
        SuspendIdle(actor);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        if (faceLeft is { } left)
            Face(sprite, left);
        PrepareActorClip(actor, sprite, clip);
        var frame = clip.FrameAt(0);
        SetFrame(sprite, clip.Asset, frame);
        SortAnimatedStanding();
        return new(clip, elapsed =>
        {
            var nextFrame = clip.FrameAt(elapsed);
            if (nextFrame == frame)
                return;
            SetFrame(sprite, clip.Asset, nextFrame);
            frame = nextFrame;
            SortAnimatedStanding();
        });
    }

    private ActorActionVisual PrepareActorAction(Node2D actor, ActorAction policy, Vector2? target)
    {
        var anchor = Projection.Project(actor.GetMeta("logical_contact").AsVector2());
        var direction = ((target ?? anchor) - anchor).Normalized();
        var pose = PrepareActorPose(actor, policy.Action, direction.X == 0 ? null : direction.X < 0);
        var clip = pose.Clip;
        var release = clip.RequireRelease();
        actor.Position = anchor;
        var actorFinished = false;
        void Sample(double elapsed)
        {
            if (!actorFinished)
            {
                if (elapsed >= clip.Length)
                {
                    actor.Position = anchor;
                    ResumeIdle(actor);
                    actorFinished = true;
                }
                else
                {
                    pose.Sample(elapsed);
                    var travel = policy.Motion == ActorMotion.Stationary ? 0 : elapsed < release
                        ? Math.Sin(elapsed / release * (Math.PI / 2))
                        : Math.Cos((elapsed - release) / (clip.Length - release) * (Math.PI / 2));
                    actor.Position = anchor + direction * (float)(7 * travel);
                    SortAnimatedStanding();
                }
            }
        }
        return new(clip, Sample);
    }

    private async Task<bool> AnimateActorAction(Playback playback, Node2D actor, ActorAction policy,
        Vector2? target, double amount, bool healing, bool fatal, string? topic, Node2D? recipient = null)
    {
        var visual = PrepareActorAction(actor, policy, target);
        var clip = visual.Clip;
        var release = clip.RequireRelease();
        var impactEnd = target is null ? 0 : release + ImpactDuration(healing);
        if (target is not null && (!double.IsFinite(impactEnd) || impactEnd <= release))
            throw new InvalidOperationException($"Actor release cannot represent the following impact: {clip.Asset.Identity}.");
        visual.Sample(0);
        var recovery = playback.Clock.TweenAsync(clip.Length, t => visual.Sample(t * clip.Length));
        if (!await playback.Clock.WaitAsync(release))
            return false;
        if (recovery.IsFaulted)
            await recovery;
        // A single drawing cannot own both an outgoing pose and a reaction.
        if (recipient == actor && !await recovery)
            return false;
        var impact = target is { } at ? AnimateImpact(playback, at, amount, healing, false, topic, recipient) : Task.FromResult(true);
        // Fail or cancel as soon as either branch does; a broken recipient must
        // not leave input waiting for an unrelated, potentially very long clip.
        var completed = await Task.WhenAny(recovery, impact);
        if (!await completed || !await (ReferenceEquals(completed, recovery) ? impact : recovery))
            return false;
        return target is null || !fatal || await playback.Clock.WaitAsync(40);
    }

    private void PrepareActorClip(Node2D actor, Sprite2D sprite, ActorClip clip)
    {
        var entries = CurrentStanding().ToArray();
        var moving = entries.Single(entry => entry.Node == actor);
        // Sample through the existing geometry owner before any frame is drawn:
        // alternate contacts need terrain cuts as well as a new actor Z index.
        var depths = ActorClipDepths(moving, sprite, clip);
        PrepareFiniteActorDepths(actor, depths);
    }

    private void PrepareFiniteActorDepths(Node2D actor, IEnumerable<float> depths)
    {
        var playback = _playback ?? throw new InvalidOperationException("Finite actor geometry requires active playback.");
        playback.ActorDepths[actor] = depths.ToArray();
        PrepareStandingPartitions(CurrentStanding());
    }

    private async Task<bool> AnimateDeath(Playback playback, int id)
    {
        var actor = DrawnUnit(id);
        if (actor is null || !WasVisible(playback, id))
            return true;
        var visual = PrepareActorPose(actor, "death");
        var at = actor.Position;
        if (!await playback.Clock.TweenAsync(visual.Clip.Length, t =>
        {
            visual.Sample(t * visual.Clip.Length);
            actor.SelfModulate = new Color(1, 1, 1, (float)(1 - t));
            SwellActor(actor, at, (float)(1 - 0.35 * t));
        }))
            return false;
        playback.ActorDepths.Remove(actor);
        _standing.RemoveAll(entry => entry.Node == actor);
        RemoveEffect(actor);
        RenderedUnitCount--;
        return true;
    }

    private Task<bool> AnimateCelebration(Playback playback, BattleConclusion conclusion)
    {
        var winners = Objects(playback.After, "players")
            .Where(player => conclusion.Team is { } team && Integer(player, "team") == team)
            .Select(player => Integer(player, "id")).ToHashSet();
        var actors = new List<(Node2D Actor, ActorActionVisual Pose)>();
        foreach (var unit in Objects(playback.After, "units"))
        {
            var id = Integer(unit, "id");
            if (Number(unit["hp"], "unit hp") <= 0 || !winners.Contains(Integer(unit, "owner"))
                || !BattleEventVisibility.UnitWasAndIsVisible(id, playback.Transition.BeforeVisibleUnitIds, playback.AfterVisible)
                || DrawnUnit(id) is not { } actor)
                continue;
            actors.Add((actor, PrepareActorPose(actor, "celebrate")));
        }
        // One terminal event starts the visible winning cohort together. The
        // existing result beat overlaps the clips rather than delaying each actor.
        var duration = Math.Max(220, actors.Select(item => item.Pose.Clip.Length).DefaultIfEmpty(0).Max());
        return playback.Clock.TweenAsync(duration, t =>
        {
            var elapsed = t * duration;
            for (var index = actors.Count - 1; index >= 0; index--)
            {
                var (actor, pose) = actors[index];
                if (elapsed < pose.Clip.Length)
                    pose.Sample(elapsed);
                else
                {
                    ResumeIdle(actor);
                    actors.RemoveAt(index);
                }
            }
        });
    }

    private async Task<bool> AnimateSpawn(Playback playback, int id)
    {
        var actor = DrawnUnit(id);
        if (actor is null)
            return true;
        SuspendIdle(actor);
        var at = actor.Position;
        if (!await playback.Clock.TweenAsync(240, t =>
        {
            actor.SelfModulate = new Color(1, 1, 1, (float)t);
            SwellActor(actor, at, (float)(0.6 + 0.4 * t));
        }))
            return false;
        actor.Modulate = Colors.White;
        SwellActor(actor, at, 1);
        ResumeIdle(actor);
        return true;
    }

    private async Task<bool> Announce(Playback playback, string message, Color color)
    {
        var banner = new Control
        {
            Name = "TurnAnnouncement",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        playback.ViewportEffects.AddChild(banner);
        banner.SetAnchorAndOffset(Side.Left, 0, 0);
        banner.SetAnchorAndOffset(Side.Right, 1, 0);
        banner.SetAnchorAndOffset(Side.Bottom, 0.5f, 17);
        banner.SetAnchorAndOffset(Side.Top, 0.5f, -17);
        var band = new Control
        {
            Name = "Band",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        banner.AddChild(band);
        band.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        band.SetAnchorAndOffset(Side.Left, -0.3f, 0);
        band.SetAnchorAndOffset(Side.Right, 0.7f, 0);
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.22f, 0.78f, 1f },
            Colors = new[] { new Color(color, 0), new Color(color, 0.88f), new Color(color, 0.88f), new Color(color, 0) }
        };
        var fill = new TextureRect
        {
            Name = "Fill",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 256,
                Height = 34,
                FillFrom = Vector2.Zero,
                FillTo = Vector2.Right
            }
        };
        band.AddChild(fill);
        fill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var (y, lineColor) in new[] { (0f, new Color(1, 1, 1, 0.4f)), (33f, new Color(0, 0, 0, 0.32f)) })
        {
            var line = new ColorRect { Color = lineColor, MouseFilter = Control.MouseFilterEnum.Ignore };
            band.AddChild(line);
            line.SetAnchorAndOffset(Side.Right, 1, 0);
            line.OffsetTop = y;
            line.OffsetBottom = y + 1;
        }
        var label = NumberLabel(message, Colors.White, new Color(0, 0, 0, 0.35f), 24, 15, 0);
        band.AddChild(label);
        label.SetAnchorAndOffset(Side.Right, 1, 0);
        try
        {
            if (!await playback.Clock.TweenAsync(220, t =>
            {
                var offset = (float)(-0.3 * (1 - Ease(t)));
                band.SetAnchorAndOffset(Side.Left, offset, 0);
                band.SetAnchorAndOffset(Side.Right, 1 + offset, 0);
                band.Modulate = new Color(1, 1, 1, (float)Ease(t));
            }))
                return false;
            if (!await playback.Clock.WaitAsync(420))
                return false;
            return await playback.Clock.TweenAsync(220, t => banner.Modulate = new Color(1, 1, 1, (float)(1 - t)));
        }
        finally { RemoveEffect(banner); }
    }

    private void PresentRecruit(Playback playback, JsonObject item)
    {
        var id = Integer(item, "unit");
        if (DrawnUnit(id) is not null)
            return;
        var snapshot = ReadProjection(playback.After);
        var unit = snapshot.Units.SingleOrDefault(unit => unit.Id == id);
        if (unit is null)
            return;
        ProjectionUnitCount++;
        if (playback.AfterVisible is not null && !playback.AfterVisible.Contains(id))
            return;
        var at = Coordinate(item["at"]!);
        var diagnostics = _diagnostics.ToList();
        var entry = CreateUnitVisual(unit with
        {
            X = at.X,
            Y = at.Y
        }, snapshot, diagnostics);
        _diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
        if (entry is null)
            return;
        _projection!.AddChild(entry.Node);
        _standing.Add(entry);
        RenderedUnitCount++;
        PrepareAnimatedPath(entry.Node, new[] { entry.Contact });
        SortAnimatedStanding();
    }

    private void RefreshCapturedSites(Playback playback)
    {
        if (_projection is null)
            return;
        var map = (JsonObject)playback.After["map"]!;
        var owners = (JsonArray)map["owners"]!;
        foreach (var site in _projection.GetChildren().OfType<Node2D>().Where(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)))
        {
            var index = int.Parse(site.Name.ToString()[13..], CultureInfo.InvariantCulture);
            var owner = Integer(owners[index], "site owner");
            if (site.GetNodeOrNull<Node2D>("OwnerBanner") is { } old)
                RemoveEffect(old);
            if (owner != 0)
            {
                var player = Objects(playback.After, "players").Single(value => Integer(value, "id") == owner);
                site.AddChild(DynamicStateView.CreateOwnerBanner(Text(player, "color"), _shadows));
            }
        }
    }

    private RuntimeAsset UnitAsset(Node2D actor) => _assets!.ResolveUnit(actor.GetMeta("unit_type").AsString(),
        actor.HasMeta("actor_key") ? actor.GetMeta("actor_key").AsString() : null);
    private Node2D? DrawnUnit(int id) => _projection?.GetNodeOrNull<Node2D>($"Unit_{id}");
    private static bool WasVisible(Playback playback, int id) => BattleEventVisibility.IsVisible(id, playback.Transition.BeforeVisibleUnitIds);
    private static JsonObject? FindUnit(JsonObject state, int id) => Objects(state, "units").FirstOrDefault(unit => Integer(unit, "id") == id);
    private static IEnumerable<JsonObject> Objects(JsonObject state, string key) => ((JsonArray)state[key]!).OfType<JsonObject>();
    private static JsonObject ReadState(JsonElement value) => Empire.Core.Determinism.EcmaJson.Parse(value.GetRawText()) as JsonObject
        ?? throw new InvalidOperationException("Presentation transition requires JSON objects.");
    private static Vector2I Coordinate(JsonNode value) => new(Integer(value["x"], "x"), Integer(value["y"], "y"));
    private Vector2 CellCentre(Vector2I at) => Projection.CellCenter(new Empire.Core.Geometry.Coord(at.X, at.Y));
    private Vector2? ImpactOn(int id) => DrawnUnit(id) is { } actor
        ? Projection.Project(actor.GetMeta("logical_contact").AsVector2()) : null;
    private static double Ease(double t) => t < 0.5 ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t);
    private static void Face(Sprite2D sprite, bool left) => sprite.Scale = new Vector2(Mathf.Abs(sprite.Scale.X) * (left ? -1 : 1), sprite.Scale.Y);
    private void SetFrame(Sprite2D sprite, RuntimeAsset asset, int index)
    {
        _assets!.ApplyFrame(sprite, asset, index);
        sprite.GetNodeOrNull<SpriteShadows>("SourceSpriteShadows")?.Synchronize(sprite, ShadowKind.Figure, asset.PixelDensity);
        sprite.SetMeta("frame", index);
    }
    internal static int StripFrame(RuntimeAsset asset, double milliseconds)
    {
        var frame = Math.Max(0, (int)Math.Floor(milliseconds * asset.FramesPerSecond!.Value / 1000));
        return asset.Loop ? frame % asset.FrameCount : Math.Min(frame, asset.FrameCount - 1);
    }
    private IEnumerable<StandingEntry> CurrentStanding() => _standing
        .Where(entry => GodotObject.IsInstanceValid(entry.Node) && !entry.Node.IsQueuedForDeletion())
        .Select(entry => entry.Node.HasMeta("logical_contact") ? entry with { Contact = entry.Node.GetMeta("logical_contact").AsVector2() } : entry);
    private void SortAnimatedStanding() => OrderStanding(CurrentStanding().ToList());
    private static void SwellActor(Node2D actor, Vector2 contact, float scale)
    {
        actor.Scale = Vector2.One * scale;
        actor.Position = contact + new Vector2(0, -15) * (1 - scale);
    }
    private static Label NumberLabel(string text, Color color, Color outline, float baseline, int size = 13, float width = 128)
    {
        var label = new Label
        {
            Name = "Number",
            Text = text,
            Position = new Vector2(-width / 2, baseline - ThemeDB.FallbackFont.GetAscent(size)),
            Size = new Vector2(width, ThemeDB.FallbackFont.GetHeight(size)),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", outline);
        label.AddThemeConstantOverride("outline_size", 3);
        return label;
    }
    private static void RemoveEffect(Node node)
    {
        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
            return;
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }
}
