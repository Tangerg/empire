#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    private sealed record Playback(BattlePresentationTransition Transition, JsonObject Before, JsonObject After,
        IReadOnlySet<int>? AfterVisible, PresentationTimeline Clock, Node2D Effects);
    private Playback? _playback;
    private bool _exiting;

    public bool IsPresenting => _playback is not null;
    public bool AnimationsEnabled { get; set; } = true;
    public event Action? PresentationRepaintRequested;
    public event Action<BattlePresentationTransition, int>? EventPresented;
    internal bool AdvanceAnimationsAutomatically { get; set; } = true;

    public override void _EnterTree() => _exiting = false;

    public override void _Process(double delta)
    {
        if (AdvanceAnimationsAutomatically) AdvancePresentation(delta);
    }

    internal void AdvancePresentation(double seconds) => _playback?.Clock.Advance(seconds);

    public override void _ExitTree()
    {
        _exiting = true;
        CancelPresentation();
    }

    public void CancelPresentation()
    {
        var playback = _playback;
        if (playback is null) return;
        _playback = null;
        playback.Clock.Cancel();
        RemoveEffect(playback.Effects);
        if (!_exiting) RenderState(playback.After, playback.AfterVisible);
    }

    /// <summary>Plays committed facts; it cannot dispatch, forecast, or edit canonical state.</summary>
    public async Task<bool> PresentTransitionAsync(BattlePresentationTransition transition,
        IReadOnlySet<int>? afterVisibleUnitIds = null)
    {
        if (IsPresenting) throw new InvalidOperationException("A battle transition is already being presented.");
        if (_exiting || !IsInsideTree()) return false;
        var before = ReadState(transition.Before);
        var after = ReadState(transition.After);
        var action = ReadState(transition.Action);
        var events = transition.Events.Select(ReadState).ToArray();
        RenderState(before, transition.BeforeVisibleUnitIds);
        var effects = new Node2D { Name = "BattleEffects", ZIndex = 2500 };
        AddChild(effects);
        var playback = new Playback(transition, before, after, afterVisibleUnitIds,
            new PresentationTimeline { MotionEnabled = AnimationsEnabled }, effects);
        _playback = playback;
        try
        {
            // Source game.ts marches the complete command path before its event
            // presenters, including before a parting shot. The move event is not
            // a second march. Only an accepted receipt reaches this adapter.
            if (Text(action, "kind") == "command" && action["path"] is JsonArray path && path.Count > 1)
                if (!await AnimateMove(playback, Integer(action, "unit"), path)) return false;
            for (var index = 0; index < events.Length; index++)
            {
                if (!ReferenceEquals(_playback, playback)) return false;
                if (!await PresentEvent(playback, events[index], transition.EventDescriptions[index])) return false;
                if (!ReferenceEquals(_playback, playback)) return false;
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
                if (!_exiting) RenderState(after, afterVisibleUnitIds);
            }
        }
    }

    private async Task<bool> PresentEvent(Playback playback, JsonObject item, string description)
    {
        switch (Text(item, "type"))
        {
            case "attack": case "counter": case "supportAttack": case "partingShot": case "areaAttack":
            {
                var defender = Integer(item, "defender");
                if (!WasVisible(playback, defender)) return true;
                var at = Text(item, "type") == "partingShot" ? Coordinate(item["at"]!) : ImpactOn(playback, defender);
                return await PlayStrike(playback, item, at, Text(item, "type") != "areaAttack", "killed");
            }
            case "attackStructure": case "areaAttackStructure":
            {
                if (!WasVisible(playback, Integer(item, "attacker"))) return true;
                var structure = Objects(playback.After, "structures").FirstOrDefault(value => Text(value, "id") == Text(item, "structure"));
                return await PlayStrike(playback, item, structure is null ? null : Coordinate(structure),
                    Text(item, "type") == "attackStructure", "destroyed");
            }
            case "death":
                return await AnimateDeath(playback, Integer(item, "unit"));
            case "heal":
            {
                var target = Integer(item, "target");
                var unit = FindUnit(playback.After, target);
                return unit is null || !WasVisible(playback, target)
                    || await AnimateImpact(playback, Coordinate(unit), Number(item["amount"], "heal amount"), true, false, _assets!.ResolveHealEffect()?.TopicId);
            }
            case "recruit":
                Repaint(playback);
                return await AnimateSpawn(playback, Integer(item, "unit"));
            case "capture":
                // Source capture refreshes map ownership, not the entire actor set.
                // The settled snapshot remains the sole owner of every captured cell.
                if (item["captured"]!.GetValue<bool>()) RefreshCapturedSites(playback);
                return true;
            case "turnStart":
            {
                var player = Objects(playback.After, "players").FirstOrDefault(value => Integer(value, "id") == Integer(item, "player"));
                if (player is null) return true;
                Repaint(playback);
                return await Announce(playback, description, Color.FromHtml(Text(player, "color")));
            }
            case "gameOver":
                return await playback.Clock.WaitAsync(220);
            default:
                // The source registry deliberately leaves these events without an
                // animation, including casts, forcedMove and non-death departures.
                return true;
        }
    }

    private async Task<bool> PlayStrike(Playback playback, JsonObject item, Vector2I? at, bool windup, string fatalField)
    {
        var attacker = FindUnit(playback.After, Integer(item, "attacker"));
        if (windup && attacker is not null && WasVisible(playback, Integer(item, "attacker")))
            if (!await AnimateStrike(playback, attacker, at ?? Coordinate(attacker))) return false;
        if (at is null) return true;
        var effect = _assets!.ResolveWeaponEffect(Text(item, "weapon"));
        return await AnimateImpact(playback, at.Value, Number(item["damage"], "damage"), false,
            item[fatalField]!.GetValue<bool>(), effect?.TopicId);
    }

    private async Task<bool> AnimateMove(Playback playback, int id, JsonArray path)
    {
        var actor = DrawnUnit(id);
        var unit = FindUnit(playback.Before, id);
        if (actor is null || unit is null || !WasVisible(playback, id)) return true;
        var asset = UnitAsset(unit);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var walk = new[] { AssetCatalog.FrameIndex(asset, "walk-a"), AssetCatalog.FrameIndex(asset, "walk-b") };
        var duration = playback.Transition.IsComputerAction ? 65.0 : 85.0;
        var elapsed = 0.0;
        SetFrame(sprite, asset, walk[0]);
        for (var index = 1; index < path.Count; index++)
        {
            var from = Coordinate(path[index - 1]!);
            var to = Coordinate(path[index]!);
            if (to.X != from.X) Face(sprite, to.X < from.X);
            var start = CellContact(from.X, from.Y);
            var end = CellContact(to.X, to.Y);
            if (!await playback.Clock.TweenAsync(duration, t =>
            {
                actor.Position = start.Lerp(end, (float)Ease(t)) - new Vector2(0, (float)(Math.Sin(Math.PI * t) * 2.5));
                SetFrame(sprite, asset, walk[(int)Math.Floor((elapsed + t * duration) * 6.25 / 1000) % 2]);
                SortAnimatedStanding();
            })) return false;
            elapsed += duration;
        }
        SetFrame(sprite, asset, AssetCatalog.FrameIndex(asset, "idle"));
        return true;
    }

    private async Task<bool> AnimateStrike(Playback playback, JsonObject attacker, Vector2I target)
    {
        var actor = DrawnUnit(Integer(attacker, "id"));
        if (actor is null) return true;
        var asset = UnitAsset(attacker);
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var at = Coordinate(attacker);
        var direction = new Vector2(Math.Sign(target.X - at.X), Math.Sign(target.Y - at.Y));
        if (direction.X != 0) Face(sprite, direction.X < 0);
        var anchor = CellContact(at.X, at.Y);
        actor.Position = anchor;
        SetFrame(sprite, asset, AssetCatalog.FrameIndex(asset, asset.Category == "mission-unit" ? "carry-or-work" : "attack"));
        if (!await playback.Clock.TweenAsync(150, t =>
        {
            actor.Position = anchor + direction * (float)(Math.Sin(Math.PI * t) * 7);
            SortAnimatedStanding();
        })) return false;
        SetFrame(sprite, asset, AssetCatalog.FrameIndex(asset, "idle"));
        actor.Position = anchor;
        SortAnimatedStanding();
        return true;
    }

    private async Task<bool> AnimateImpact(Playback playback, Vector2I at, double amount, bool healing, bool fatal, string? topic)
    {
        var effect = new Node2D { Name = healing ? "Heal" : "Hit", Position = CellCentre(at) };
        playback.Effects.AddChild(effect);
        RuntimeAsset? art = topic is null ? null : _assets!.ResolveEffect(topic);
        Sprite2D? sprite = art is null ? null : _assets!.CreateSpriteAtFrame(art, 0);
        if (sprite is not null)
        {
            sprite.Name = "Strip";
            sprite.Scale = Vector2.One / art!.PixelDensity;
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
            burst = new Polygon2D { Name = "Burst", Color = new Color(1, 1, 1, 0.65f),
                Polygon = Enumerable.Range(0, 48).Select(index => Vector2.FromAngle(index * Mathf.Tau / 48) * 12).ToArray() };
            effect.AddChild(burst);
            effect.MoveChild(burst, sprite is null ? 0 : 1);
        }
        var duration = healing ? 500.0 : 420.0;
        try
        {
            if (!await playback.Clock.TweenAsync(duration, t =>
            {
                if (sprite is not null) SetFrame(sprite, art!, StripFrame(art!, t * duration));
                number.Position = origin + new Vector2(0, (float)(-(healing ? 16 : 14) * t));
                number.Modulate = new Color(1, 1, 1, (float)(1 - t * t));
                if (burst is not null)
                {
                    burst.Scale = Vector2.One * (float)(1 + t * 0.83);
                    burst.Modulate = new Color(1, 1, 1, (float)(0.8 * (1 - t)));
                }
            })) return false;
        }
        finally { RemoveEffect(effect); }
        return !fatal || await playback.Clock.WaitAsync(40);
    }

    private async Task<bool> AnimateDeath(Playback playback, int id)
    {
        var actor = DrawnUnit(id);
        if (actor is null || !WasVisible(playback, id)) return true;
        var at = actor.Position;
        if (!await playback.Clock.TweenAsync(220, t =>
        {
            actor.SelfModulate = new Color(1, 1, 1, (float)(1 - t));
            SwellActor(actor, at, (float)(1 - 0.35 * t));
        })) return false;
        _standing.RemoveAll(entry => entry.Node == actor);
        RemoveEffect(actor);
        RenderedUnitCount--;
        return true;
    }

    private async Task<bool> AnimateSpawn(Playback playback, int id)
    {
        var actor = DrawnUnit(id);
        if (actor is null) return true;
        var at = actor.Position;
        if (!await playback.Clock.TweenAsync(240, t =>
        {
            actor.SelfModulate = new Color(1, 1, 1, (float)t);
            SwellActor(actor, at, (float)(0.6 + 0.4 * t));
        })) return false;
        actor.Modulate = Colors.White;
        SwellActor(actor, at, 1);
        return true;
    }

    private async Task<bool> Announce(Playback playback, string message, Color color)
    {
        var banner = new Node2D { Name = "TurnAnnouncement", Position = new Vector2(0, FieldSize.Y / 2 - 17) };
        var band = new Node2D { Name = "Band" };
        var gradient = new Gradient { Offsets = new[] { 0f, 0.22f, 0.78f, 1f },
            Colors = new[] { new Color(color, 0), new Color(color, 0.88f), new Color(color, 0.88f), new Color(color, 0) } };
        band.AddChild(new Sprite2D { Centered = false, Texture = new GradientTexture2D {
            Gradient = gradient, Width = (int)FieldSize.X, Height = 34, FillFrom = Vector2.Zero, FillTo = Vector2.Right } });
        foreach (var (y, lineColor) in new[] { (0f, new Color(1, 1, 1, 0.4f)), (33f, new Color(0, 0, 0, 0.32f)) })
            band.AddChild(new Line2D { Points = new[] { new Vector2(0, y), new Vector2(FieldSize.X, y) }, Width = 1, DefaultColor = lineColor });
        var label = NumberLabel(message, Colors.White, new Color(0, 0, 0, 0.35f), 24, 15, FieldSize.X);
        label.Position = new Vector2(0, label.Position.Y);
        band.AddChild(label);
        banner.AddChild(band);
        playback.Effects.AddChild(banner);
        try
        {
            if (!await playback.Clock.TweenAsync(220, t =>
            {
                band.Position = new Vector2((float)(-FieldSize.X * 0.3 * (1 - Ease(t))), 0);
                band.Modulate = new Color(1, 1, 1, (float)Ease(t));
            })) return false;
            if (!await playback.Clock.WaitAsync(420)) return false;
            return await playback.Clock.TweenAsync(220, t => banner.Modulate = new Color(1, 1, 1, (float)(1 - t)));
        }
        finally { RemoveEffect(banner); }
    }

    private void Repaint(Playback playback)
    {
        RenderState(playback.After, playback.AfterVisible);
        PresentationRepaintRequested?.Invoke();
    }

    private void RefreshCapturedSites(Playback playback)
    {
        if (_projection is null) return;
        var map = (JsonObject)playback.After["map"]!;
        var owners = (JsonArray)map["owners"]!;
        var tiles = (JsonArray)map["tiles"]!;
        foreach (var site in _projection.GetChildren().OfType<Node2D>().Where(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)))
        {
            var index = int.Parse(site.Name.ToString()[13..], CultureInfo.InvariantCulture);
            var owner = Integer(owners[index], "site owner");
            var asset = _assets!.ResolveMapStructure(tiles[index]!.GetValue<string>());
            site.GetNode<Sprite2D>("AuthoredSprite").Texture = _assets.CreateFrameTexture(asset, AssetCatalog.FrameIndex(asset, owner == 0 ? "normal" : "captured"));
            if (site.GetNodeOrNull<Node2D>("OwnerBanner") is { } old) RemoveEffect(old);
            if (owner != 0)
            {
                var player = Objects(playback.After, "players").Single(value => Integer(value, "id") == owner);
                site.AddChild(DynamicStateView.CreateOwnerBanner(Text(player, "color")));
            }
        }
    }

    private RuntimeAsset UnitAsset(JsonObject unit) => _assets!.ResolveUnit(Text(unit, "type"), unit["key"]?.GetValue<string>());
    private Node2D? DrawnUnit(int id) => _projection?.GetNodeOrNull<Node2D>($"Unit_{id}");
    private static bool WasVisible(Playback playback, int id) => playback.Transition.BeforeVisibleUnitIds?.Contains(id) ?? true;
    private static JsonObject? FindUnit(JsonObject state, int id) => Objects(state, "units").FirstOrDefault(unit => Integer(unit, "id") == id);
    private static IEnumerable<JsonObject> Objects(JsonObject state, string key) => ((JsonArray)state[key]!).OfType<JsonObject>();
    private static JsonObject ReadState(JsonElement value) => Empire.Core.Determinism.EcmaJson.Parse(value.GetRawText()) as JsonObject
        ?? throw new InvalidOperationException("Presentation transition requires JSON objects.");
    private static Vector2I Coordinate(JsonNode value) => new(Integer(value["x"], "x"), Integer(value["y"], "y"));
    private static Vector2 CellCentre(Vector2I at) => new(at.X * 32 + 16, at.Y * 32 + 16);
    private static Vector2I? ImpactOn(Playback playback, int id)
    {
        var unit = FindUnit(playback.After, id) ?? FindUnit(playback.Before, id);
        return unit is null ? null : Coordinate(unit);
    }
    private static double Ease(double t) => t < 0.5 ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t);
    private static void Face(Sprite2D sprite, bool left) => sprite.Scale = new Vector2(Mathf.Abs(sprite.Scale.X) * (left ? -1 : 1), sprite.Scale.Y);
    private void SetFrame(Sprite2D sprite, RuntimeAsset asset, int index)
    {
        sprite.Texture = _assets!.CreateFrameTexture(asset, index);
        sprite.GetNodeOrNull<CandidateSpriteShadows>("SourceSpriteShadows")?.Synchronize(sprite, CandidateShadowKind.Figure, asset.PixelDensity);
        sprite.SetMeta("frame", index);
    }
    internal static int StripFrame(RuntimeAsset asset, double milliseconds)
    {
        var frame = Math.Max(0, (int)Math.Floor(milliseconds * asset.FramesPerSecond!.Value / 1000));
        return asset.Loop ? frame % asset.FrameCount : Math.Min(frame, asset.FrameCount - 1);
    }
    private void SortAnimatedStanding() => OrderStanding(_standing.Where(entry => GodotObject.IsInstanceValid(entry.Node) && !entry.Node.IsQueuedForDeletion())
        .Select(entry => entry with { Contact = entry.Node.Position }).ToList());
    private static void SwellActor(Node2D actor, Vector2 contact, float scale)
    {
        actor.Scale = Vector2.One * scale;
        actor.Position = contact + new Vector2(0, -15) * (1 - scale);
    }
    private static Label NumberLabel(string text, Color color, Color outline, float baseline, int size = 13, float width = 128)
    {
        var label = new Label { Name = "Number", Text = text, Position = new Vector2(-width / 2, baseline - ThemeDB.FallbackFont.GetAscent(size)),
            Size = new Vector2(width, ThemeDB.FallbackFont.GetHeight(size)), HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", outline);
        label.AddThemeConstantOverride("outline_size", 3);
        return label;
    }
    private static void RemoveEffect(Node node)
    {
        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion()) return;
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }
}
