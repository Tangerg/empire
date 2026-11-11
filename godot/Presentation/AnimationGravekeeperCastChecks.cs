#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckGravekeeperCastPlayback()
    {
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        Require(bindings.Units.Count + bindings.Actors.Count == 40,
            "The cast slice retains all forty generic and named-actor bindings.");
        var clip = assets.ResolveUnitClip("c01.gravekeeper", "attack", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds);
        var original = assets.ResolveUnit("c01.gravekeeper");
        Require(bindings.Units["c01.gravekeeper"].ActionClips["attack"] == "gravekeeper.channel"
            && clip.Asset.Identity == "gravekeeper-cast-v1" && clip.Length == 600 && clip.RequireRelease() == 300
            && !clip.ShowAccent && clip.Death is null && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .0921053f)
            && clip.Asset.TopicId == original.TopicId,
            "The generic gravekeeper retains its authored 600 ms channel and exact release, scale and topic.");
        Require(original.Identity == "C01-FFV1-UNIT-GRAVEKEEPER" && original.TopicId == "C01-UNIT-GRAVEKEEPER"
            && idle.Asset.Identity == "gravekeeper-breathe-v1" && idle.Asset.TopicId == original.TopicId
            && idle.Asset != original,
            "Cast recovery selects the declared breathing appearance without replacing the canonical gravekeeper sheet.");
        foreach (var (action, expected) in new[] { ("idle", "gravekeeper.breathe"), ("move", "legacy.move"),
            ("hit", "gravekeeper.recoil"), ("death", "gravekeeper.fall"), ("celebrate", "static.celebrate") })
            Require(bindings.Units["c01.gravekeeper"].ActionClips[action] == expected,
                "Casting does not replace the gravekeeper's other declared actions.");
        var starts = new[] { 0.0, 90, 190, 300, 400, 500 };
        Require(clip.Asset.FrameOrder.SequenceEqual(new[] { "cast-ready", "cast-gather", "cast-raise", "cast-release", "cast-recover", "cast-rest" }),
            "The channel declares six ordered original anatomical poses.");
        var pictures = new HashSet<string>();
        for (var frame = 0; frame < starts.Length; frame++)
        {
            Require(clip.FrameAt(starts[frame]) == frame
                && clip.FrameAt(frame == 5 ? 599.999 : starts[frame + 1] - .001) == frame,
                "Every production cast pose has its exact half-open interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, frame).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each original cast picture is RGBA with transparent padding around the whole silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(600) == 5,
            "Six distinct authored pictures hold their final frame until the playback owner restores idle.");
        foreach (var weapon in new[] { "c01.lantern-staff", "c01.oath-dispel" })
            Require(assets.ResolveWeaponAction(weapon) == new ActorAction("attack", ActorMotion.Stationary),
                "Both gravekeeper weapon policies retain explicit stationary attack selection.");

        // Synthetic receipts exercise production art and presentation clocks, not lawful gameplay orders.
        var state = ReleaseState();
        var source = state["units"]![0]!.AsObject();
        var target = state["units"]![1]!.AsObject();
        source["type"] = "c01.gravekeeper";
        source["facing"] = "east";
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var targetX in new[] { 11, 13 })
            {
                target["x"] = targetX;
                foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot", "heal" })
                {
                    var events = GravekeeperEvents(kind, source, target);
                    var transition = Transition(state, state, IdleAction(), events);
                    var unchanged = state.ToJsonString();
                    var observed = new List<int>();
                    void Presented(BattlePresentationTransition _, int index) => observed.Add(index);
                    board.EventPresented += Presented;
                    try
                    {
                        var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                        var actor = Actor(board, aid);
                        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                        var origin = actor.Position;
                        var contact = actor.GetMeta("logical_contact").AsVector2();
                        var sign = targetX < 12 ? -1 : 1;
                        var effectPath = kind == "heal" ? "BattleEffects/Heal" : "BattleEffects/Hit";
                        Node? released = null;
                        for (var frame = 0; frame < starts.Length; frame++)
                        {
                            if (frame > 0)
                                await Tick(board, (starts[frame] - starts[frame - 1]) / 1000);
                            CheckClipSprite(sprite, clip.Asset, frame, sign);
                            CheckClipShadow(sprite);
                            Require(actor.Position == origin && actor.Scale == Vector2.One
                                && actor.SelfModulate == Colors.White && actor.Modulate == Colors.White
                                && actor.GetMeta("logical_contact").AsVector2() == contact
                                && actor.GetNodeOrNull("ActionAccent") is null,
                                "Stationary casting keeps root, contact, body color and shadow composition without a source accent.");
                            var effect = board.GetNodeOrNull(effectPath);
                            Require((effect is not null) == (frame >= 3), "Recipient feedback starts at the release pose only.");
                            if (frame == 3)
                                released = effect;
                            if (frame > 3)
                                Require(ReferenceEquals(effect, released), "Recovery cannot replay recipient feedback.");
                        }
                        await Tick(board, .100);
                        CheckSettledClipActor(board, aid, idle.Asset, origin);
                        Require(!playing.IsCompleted && ReferenceEquals(released, board.GetNodeOrNull(effectPath)),
                            "At 600 ms the declared breathing idle returns while the same recipient effect remains active.");
                        await Tick(board, kind == "heal" ? .199999 : .119999);
                        Require(!playing.IsCompleted && board.GetNodeOrNull(effectPath) is not null,
                            "Recipient duration remains 500 ms for healing and 420 ms for damage.");
                        await Tick(board, .000001);
                        Require(playing.IsCompleted && await playing && board.GetNodeOrNull("BattleEffects") is null
                            && observed.SequenceEqual(Enumerable.Range(0, events.Length)) && state.ToJsonString() == unchanged,
                            "At 800/720 ms casting settles, publishes every receipt once and preserves canonical state.");
                    }
                    finally { board.EventPresented -= Presented; }
                }
            }
            target["x"] = 13;
            await CheckGravekeeperCastBoundaries(board, state, source, target);
            await CheckGravekeeperCastScope(board, state, source, target);
            await CheckGravekeeperCastLifetime(board, state, source, target);
            await CheckHealingVisibility(board, assets, state, aid, did);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Gravekeeper cast: synthetic production-art receipts, six poses, release300/recovery600, hit720/heal800, mirrors/lifetime/privacy passed.");
    }

    private static JsonObject[] GravekeeperEvents(string kind, JsonObject source, JsonObject target)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        if (kind == "heal")
            return [HealingEvent(aid, did)];
        var strike = ReleaseStrike(kind, aid, did, kind == "counter" ? "c01.oath-dispel" : "c01.lantern-staff");
        if (kind == "partingShot")
            strike["at"] = At(Int(target, "x"), Int(target, "y"));
        return kind == "attack" ? MainAttackScope(source, target, "c01.lantern-staff", strike) : [strike];
    }

    private async Task CheckGravekeeperCastBoundaries(BoardView board, JsonObject state, JsonObject source, JsonObject target)
    {
        foreach (var kind in new[] { "attack", "heal" })
            foreach (var milliseconds in new[] { 299.999, 300, 300.001, 599.999, 600, 600.001, 1000 })
            {
                var events = GravekeeperEvents(kind, source, target);
                var observed = new List<int>();
                void Presented(BattlePresentationTransition _, int index) => observed.Add(index);
                board.EventPresented += Presented;
                try
                {
                    var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
                    await Tick(board, milliseconds / 1000);
                    var sprite = Actor(board, Int(source, "id")).GetNode<Sprite2D>("AuthoredSprite");
                    var expected = assets.ResolveUnitClip("c01.gravekeeper", milliseconds < 600 ? "attack" : "idle", ActorClipUnit.Milliseconds);
                    CheckClipSprite(sprite, expected.Asset, expected.FrameAt(milliseconds < 600 ? milliseconds : 0), 1);
                    Require((board.GetNodeOrNull(kind == "heal" ? "BattleEffects/Heal" : "BattleEffects/Hit") is not null)
                        == (milliseconds >= 300 && milliseconds < (kind == "heal" ? 800 : 720)),
                        "Production release and recovery remain exact under independent before/at/after and overshoot samples.");
                    if (milliseconds == 1000)
                        Require(playing.IsCompleted && await playing && observed.SequenceEqual(Enumerable.Range(0, events.Length)),
                            "One overshooting tick settles all source and recipient ownership with one publication per event.");
                    else
                    {
                        Require(observed.SequenceEqual(kind == "attack" && milliseconds >= 300 ? new[] { 0 } : Array.Empty<int>()),
                            "Only the activation publishes at release, before its impact receipt completes.");
                        board.CancelPresentation();
                        Require(!await playing, "Cancelled boundary samples never complete the receipt.");
                    }
                }
                finally { board.EventPresented -= Presented; }
            }
    }

    private async Task CheckGravekeeperCastScope(BoardView board, JsonObject state, JsonObject source, JsonObject target)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var events = new[] { new JsonObject { ["type"] = "castResolved", ["unit"] = aid } }
            .Concat(MainAttackScope(source, target, "c01.lantern-staff",
                ReleaseStrike("areaAttack", aid, did, "c01.lantern-staff"),
                ReleaseStrike("areaAttack", aid, did, "c01.lantern-staff"))).ToArray();
        var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
        await Tick(board, .300);
        var first = board.GetNode<Node2D>("BattleEffects/Hit");
        await Tick(board, .420);
        Require(!ReferenceEquals(first, board.GetNodeOrNull("BattleEffects/Hit"))
            && Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString()
                == assets.ResolveUnitClip("c01.gravekeeper", "idle", ActorClipUnit.Milliseconds).Asset.Identity,
            "castResolved adds no source replay and a second area victim starts only another target impact, after idle restoration.");
        await Tick(board, .420);
        Require(playing.IsCompleted && await playing, "Two victims use one 300 ms wind-up and two unchanged 420 ms impacts.");
        foreach (var sourceId in new[] { aid, int.MaxValue })
        {
            var visible = new HashSet<int> { did };
            var activation = Activation(sourceId, "c01.lantern-staff");
            playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                [activation, ReleaseStrike("attack", sourceId, did, "c01.lantern-staff"), ActivationCompleted(sourceId, "c01.lantern-staff")], visible), visible);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null
                && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "Hidden or absent production casters introduce no source pose or hidden release delay.");
            await Drain(board, playing);
        }
        var sourceOnly = new HashSet<int> { aid };
        playing = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), GravekeeperEvents("attack", source, target), sourceOnly), sourceOnly);
        await Tick(board, .300);
        Require(Frame(Actor(board, aid)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "Visible activation keeps its declared release but exposes no feedback for a hidden victim.");
        await Tick(board, .300);
        Require(playing.IsCompleted && await playing,
            "A hidden victim adds no recipient lifetime beyond the visible source recovery.");
        playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [new JsonObject { ["type"] = "regen", ["unit"] = aid, ["amount"] = 20 }]));
        Require(Frame(Actor(board, aid)) == 0 && board.GetNodeOrNull("BattleEffects/Heal") is not null,
            "Regeneration on a production gravekeeper remains immediate source-free feedback, not a channel action.");
        await Tick(board, .500);
        Require(playing.IsCompleted && await playing, "Gravekeeper regeneration retains its independent 500 ms lifetime.");
    }

    private async Task CheckGravekeeperCastLifetime(BoardView board, JsonObject state, JsonObject source, JsonObject target)
    {
        var aid = Int(source, "id");
        var origin = board.Projection.CellContact(new(12, 16));
        foreach (var kind in new[] { "attack", "heal" })
        {
            var transition = Transition(state, state, IdleAction(), GravekeeperEvents(kind, source, target));
            foreach (var seconds in new[] { .190, .400, .650 })
            {
                var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, seconds);
                board.CancelPresentation();
                Require(!await playing && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                    "Interruption before release, during authored recovery or during recipient-only feedback cleans both owners.");
                CheckGravekeeperIdleActor(board, aid, origin, 0);
                await Tick(board, 1);
                CheckGravekeeperIdleActor(board, aid, origin, 1000);
            }
            foreach (var exit in new[] { false, true })
            {
                var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .400);
                if (exit)
                {
                    RemoveChild(board);
                    Require(!await playing, "Tree exit cancels the released cast without another process frame.");
                    AddChild(board);
                }
                ConfigureActorClipBoard(board, assets);
                board.RenderState(state);
                CheckGravekeeperIdleActor(board, aid, origin, 0);
                await Tick(board, 1);
                Require(!await playing && board.GetNodeOrNull("BattleEffects") is null && !board.IsPresenting,
                    "Rebinding or reattaching cannot resurrect a retired production cast.");
                CheckGravekeeperIdleActor(board, aid, origin, 1000);
            }
            board.AnimationsEnabled = false;
            try
            {
                var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
                Require(reduced.IsCompleted && await reduced && board.GetNodeOrNull("BattleEffects") is null,
                    "Reduced motion settles production casting synchronously without a source pose or delayed effect.");
                CheckGravekeeperIdleActor(board, aid, origin, 0);
            }
            finally { board.AnimationsEnabled = true; }
        }
    }
}
