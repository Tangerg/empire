#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Presentation.Feedback;
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    private sealed record Playback(BattlePresentationTransition Transition, JsonObject After,
        IReadOnlySet<int>? AfterVisible, PresentationTimeline Clock, Node2D Effects, Control ViewportEffects);
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
                    return await PlayStrike(playback, item, at, Text(item, "type") != "areaAttack", "killed");
                }
            case "attackStructure":
            case "areaAttackStructure":
                {
                    if (!WasVisible(playback, Integer(item, "attacker")))
                        return true;
                    var structure = Objects(playback.After, "structures").FirstOrDefault(value => Text(value, "id") == Text(item, "structure"));
                    return await PlayStrike(playback, item, structure is null ? null : CellCentre(Coordinate(structure)),
                        Text(item, "type") == "attackStructure", "destroyed");
                }
            case "death":
                return await AnimateDeath(playback, Integer(item, "unit"));
            case "forcedMove":
                {
                    var id = Integer(item, "unit");
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
                        || await AnimateImpact(playback, at, Number(item["amount"], "collision amount"), false, item["killed"]!.GetValue<bool>(), null);
                }
            case "heal":
                {
                    var target = Integer(item, "target");
                    var unit = FindUnit(playback.After, target);
                    return unit is null || !WasVisible(playback, target) || ImpactOn(target) is not { } at
                        || await AnimateImpact(playback, at, Number(item["amount"], "heal amount"), true, false, _assets!.ResolveHealEffect()?.TopicId);
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
                return await playback.Clock.WaitAsync(220);
            default:
                // The source registry deliberately leaves these events without an
                // animation, including casts and non-death departures.
                return true;
        }
    }

    private async Task<bool> PlayStrike(Playback playback, JsonObject item, Vector2? at, bool windup, string fatalField)
    {
        var attacker = Integer(item, "attacker");
        if (windup && WasVisible(playback, attacker) && DrawnUnit(attacker) is { } actor)
            if (!await AnimateStrike(playback, actor, at ?? Projection.Project(actor.GetMeta("logical_contact").AsVector2())))
                return false;
        if (at is null)
            return true;
        var effect = _assets!.ResolveWeaponEffect(Text(item, "weapon"));
        return await AnimateImpact(playback, at.Value, Number(item["damage"], "damage"), false,
            item[fatalField]!.GetValue<bool>(), effect?.TopicId);
    }

    private async Task<bool> AnimatePath(Playback playback, int id, JsonArray path, bool walking)
    {
        var actor = DrawnUnit(id);
        if (actor is null || path.Count < 2 || !WasVisible(playback, id))
            return true;
        SuspendIdle(actor);
        var asset = UnitAsset(actor);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var walk = new[] { AssetCatalog.FrameIndex(asset, "walk-a"), AssetCatalog.FrameIndex(asset, "walk-b") };
        // Presentation pace only: one support phase per travelled cell. The committed
        // receipt still owns the path, destination and event order. A single tween
        // preserves progress across cell boundaries instead of losing frame overshoot.
        var duration = walking ? playback.Transition.IsComputerAction ? 110.0 : 160.0 : 120.0;
        var contacts = path.Select(point => Coordinate(point!))
            .Select(point => LogicalContact(point.X, point.Y)).ToArray();
        PrepareAnimatedPath(actor, contacts);
        var steps = contacts.Length - 1;
        SetFrame(sprite, asset, walking ? walk[0] : AssetCatalog.FrameIndex(asset, "idle"));
        if (!await playback.Clock.TweenAsync(duration * steps, t =>
        {
            var distance = t * steps;
            var segment = Math.Min((int)Math.Floor(distance), steps - 1);
            var fraction = (float)(distance - segment);
            var start = contacts[segment];
            var end = contacts[segment + 1];
            var visualDirection = Projection.Project(end).X - Projection.Project(start).X;
            if (visualDirection != 0)
                Face(sprite, visualDirection < 0);
            var logical = start.Lerp(end, fraction);
            // Keep the actor's support plane and its composited contact shadow on
            // the road. The drawn walk poses provide the gait, not a whole-body hop.
            PlaceAnimatedActor(actor, logical);
            SetFrame(sprite, asset, walking ? walk[segment % walk.Length] : AssetCatalog.FrameIndex(asset, "idle"));
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

    private void PrepareAnimatedPath(Node2D actor, IReadOnlyList<Vector2> contacts)
    {
        var entries = CurrentStanding().ToArray();
        var movingEntry = entries.Single(entry => entry.Node == actor);
        var frontOffset = StandingDepth(movingEntry) - Projection.Depth(movingEntry.Contact);
        var depths = contacts.Select(contact => Projection.Depth(contact) + frontOffset).ToArray();
        var first = (int)Math.Floor(depths.Min());
        var last = (int)Math.Ceiling(depths.Max());
        PrepareStandingPartitions(entries,
            Enumerable.Range(first, last - first + 1).Select(depth => (float)depth));
    }

    private async Task<bool> AnimateStrike(Playback playback, Node2D actor, Vector2 target)
    {
        SuspendIdle(actor);
        var clip = _assets!.ResolveUnitClip(actor.GetMeta("unit_type").AsString(), "attack",
            actor.HasMeta("actor_key") ? actor.GetMeta("actor_key").AsString() : null);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var anchor = Projection.Project(actor.GetMeta("logical_contact").AsVector2());
        var direction = (target - anchor).Normalized();
        if (direction.X != 0)
            Face(sprite, direction.X < 0);
        actor.Position = anchor;
        PrepareActorClip(actor, sprite, clip);
        var frame = clip.FrameAt(0);
        SetFrame(sprite, clip.Asset, frame);
        SortAnimatedStanding();
        if (!await playback.Clock.TweenAsync(clip.DurationMs, t =>
        {
            var nextFrame = clip.FrameAt(t * clip.DurationMs);
            if (nextFrame != frame)
            {
                SetFrame(sprite, clip.Asset, nextFrame);
                frame = nextFrame;
            }
            actor.Position = anchor + direction * (float)(Math.Sin(Math.PI * t) * 7);
            SortAnimatedStanding();
        }))
            return false;
        ResumeIdle(actor);
        actor.Position = anchor;
        SortAnimatedStanding();
        return true;
    }

    private void PrepareActorClip(Node2D actor, Sprite2D sprite, ActorClip clip)
    {
        var entries = CurrentStanding().ToArray();
        var moving = entries.Single(entry => entry.Node == actor);
        // Sample through the existing geometry owner before any frame is drawn:
        // alternate contacts need terrain cuts as well as a new actor Z index.
        var depths = ActorClipDepths(moving, sprite, clip);
        PrepareStandingPartitions(entries.Where(entry => entry.Node != actor), depths);
    }

    private async Task<bool> AnimateImpact(Playback playback, Vector2 at, double amount, bool healing, bool fatal, string? topic)
    {
        var effect = new Node2D { Name = healing ? "Heal" : "Hit", Position = at };
        playback.Effects.AddChild(effect);
        RuntimeAsset? art = topic is null ? null : _assets!.ResolveEffect(topic);
        Sprite2D? sprite = art is null ? null : _assets!.CreateSpriteAtFrame(art, 0);
        if (sprite is not null)
        {
            sprite.Name = "Strip";
            sprite.SetMeta("frame", 0);
            effect.AddChild(sprite);
        }
        var number = NumberLabel((healing ? "+" : "-") + amount.ToString(CultureInfo.InvariantCulture),
            healing ? new Color("#eaffee") : Colors.White, healing ? new Color("#1c5c33") : new Color("#7a1208"), healing ? -6 : -8);
        var origin = number.Position;
        effect.AddChild(number);
        Polygon2D? burst = null;
        if (!healing)
        {
            burst = new Polygon2D
            {
                Name = "Burst",
                Color = new Color(1, 1, 1, 0.65f),
                Polygon = Enumerable.Range(0, 48).Select(index => Vector2.FromAngle(index * Mathf.Tau / 48) * 12).ToArray()
            };
            effect.AddChild(burst);
            effect.MoveChild(burst, sprite is null ? 0 : 1);
        }
        var duration = healing ? 500.0 : 420.0;
        try
        {
            if (!await playback.Clock.TweenAsync(duration, t =>
            {
                if (sprite is not null)
                    SetFrame(sprite, art!, StripFrame(art!, t * duration));
                number.Position = origin + new Vector2(0, (float)(-(healing ? 16 : 14) * t));
                number.Modulate = new Color(1, 1, 1, (float)(1 - t * t));
                if (burst is not null)
                {
                    burst.Scale = Vector2.One * (float)(1 + t * 0.83);
                    burst.Modulate = new Color(1, 1, 1, (float)(0.8 * (1 - t)));
                }
            }))
                return false;
        }
        finally { RemoveEffect(effect); }
        return !fatal || await playback.Clock.WaitAsync(40);
    }

    private async Task<bool> AnimateDeath(Playback playback, int id)
    {
        var actor = DrawnUnit(id);
        if (actor is null || !WasVisible(playback, id))
            return true;
        SuspendIdle(actor);
        var at = actor.Position;
        if (!await playback.Clock.TweenAsync(220, t =>
        {
            actor.SelfModulate = new Color(1, 1, 1, (float)(1 - t));
            SwellActor(actor, at, (float)(1 - 0.35 * t));
        }))
            return false;
        _standing.RemoveAll(entry => entry.Node == actor);
        RemoveEffect(actor);
        RenderedUnitCount--;
        return true;
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
