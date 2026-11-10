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
    private async Task CheckArcherShotPlayback()
    {
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        Require(bindings.Units.Count + bindings.Actors.Count == 40,
            "The shot slice retains all forty generic and named-actor bindings.");
        var clip = assets.ResolveUnitClip("c01.archer", "attack", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds);
        Require(bindings.Units["c01.archer"].ActionClips["attack"] == "archer.loose"
            && clip.Asset.Identity == "archer-shot-v1" && clip.Asset.TopicId == "C01-UNIT-ARCHER"
            && clip.Asset.FrameCount == 6 && clip.Length == 600 && clip.RequireRelease() == 320
            && !clip.ShowAccent && clip.Death is null && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .0893f)
            && idle.Asset.Identity == "archer-breathe-v1",
            "Only the generic archer attack selects its authored 600 ms shot; its independently authored idle remains selected.");
        foreach (var (action, expected) in new[] { ("idle", "archer.breathe"), ("move", "legacy.move"),
            ("hit", "static.hit"), ("death", "static.death"), ("celebrate", "static.celebrate") })
            Require(bindings.Units["c01.archer"].ActionClips[action] == expected,
                "Shooting does not replace the archer's other declared actions.");
        Require(!bindings.Actors.ContainsKey("campaign-elin")
            && bindings.Units["c01.archer"].AppearanceId is null
            && ReferenceEquals(assets.ResolveUnitClip("c01.archer", "attack", ActorClipUnit.Milliseconds, "campaign-elin"), clip)
            && ReferenceEquals(assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds, "campaign-elin"), idle),
            "Elin has no separate appearance or action map and inherits her canonical C01 archer art.");
        foreach (var type in new[] { "archer", "c01.bran" })
            Require(bindings.Units[type].ActionClips["attack"] == "static.attack"
                && assets.ResolveUnitClip(type, "attack", ActorClipUnit.Milliseconds).Asset == assets.ResolveUnit(type),
                "The base archer and Bran keep their original static attack artwork.");
        Require(bindings.WeaponEffects["c01.ranger-bow"] == "C01-FX-03",
            "The ranger bow retains the existing recipient impact topic; no flight owner is introduced.");
        var starts = new[] { 0.0, 100, 200, 320, 400, 500 };
        Require(clip.Asset.FrameOrder.SequenceEqual(new[] { "retrieve", "nock", "draw", "release", "recover", "rest" }),
            "The shot declares six ordered original anatomical poses.");
        var pictures = new HashSet<string>();
        for (var frame = 0; frame < starts.Length; frame++)
        {
            Require(clip.FrameAt(starts[frame]) == frame
                && clip.FrameAt(frame == 5 ? 599.999 : starts[frame + 1] - .001) == frame,
                "Every production shot pose has its exact half-open interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, frame).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each original shot picture is RGBA with transparent padding around the whole silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(600) == 5,
            "Six distinct authored pictures hold their final frame until the playback owner restores idle.");
        Require(assets.ResolveWeaponAction("c01.ranger-bow") == new ActorAction("attack", ActorMotion.Stationary),
            "The archer bow policy retains explicit stationary attack selection.");

        // Synthetic receipts exercise production art and presentation clocks, not lawful gameplay orders.
        var state = ReleaseState();
        var source = state["units"]![0]!.AsObject();
        var target = state["units"]![1]!.AsObject();
        source["type"] = "c01.archer";
        source["facing"] = "east";
        var aid = Int(source, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var targetX in new[] { 11, 13 })
            {
                target["x"] = targetX;
                foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
                {
                    var events = ArcherEvents(kind, source, target);
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
                        const string effectPath = "BattleEffects/Hit";
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
                                "Stationary shooting keeps root, contact, body color and shadow composition without a source accent.");
                            var effect = board.GetNodeOrNull(effectPath);
                            Require((effect is not null) == (frame >= 3), "Recipient feedback starts at the release pose only.");
                            if (frame == 3)
                            {
                                released = effect;
                                Require(effect is Node2D hit && hit.Position == Actor(board, Int(target, "id")).Position
                                    && hit.GetNode<Sprite2D>("Strip").GetMeta("topic_id").AsString() == "C01-FX-03",
                                    "The unchanged bow strip is recipient hit feedback at release, not a source or flying arrow.");
                            }
                            if (frame > 3)
                                Require(ReferenceEquals(effect, released), "Recovery cannot replay recipient feedback.");
                        }
                        await Tick(board, .100);
                        CheckSettledClipActor(board, aid, idle.Asset, origin);
                        Require(!playing.IsCompleted && ReferenceEquals(released, board.GetNodeOrNull(effectPath)),
                            "At 600 ms the authored idle returns while the same recipient effect remains active.");
                        await Tick(board, .139999);
                        Require(!playing.IsCompleted && board.GetNodeOrNull(effectPath) is not null,
                            "Recipient hit feedback retains its 420 ms lifetime.");
                        await Tick(board, .000001);
                        Require(playing.IsCompleted && await playing && board.GetNodeOrNull("BattleEffects") is null
                            && observed.SequenceEqual(Enumerable.Range(0, events.Length)) && state.ToJsonString() == unchanged,
                            "At 740 ms shooting settles, publishes every receipt once and preserves canonical state.");
                    }
                    finally { board.EventPresented -= Presented; }
                }
            }
            target["x"] = 13;
            await CheckArcherShotBoundaries(board, state, source, target);
            await CheckArcherShotScope(board, state, source, target);
            await CheckArcherShotLifetime(board, state, source, target);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Archer shot: synthetic production-art receipts, six poses, release320/recovery600, hit740, mirrors/lifetime/privacy passed.");
    }

    private static JsonObject[] ArcherEvents(string kind, JsonObject source, JsonObject target)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var strike = ReleaseStrike(kind, aid, did, "c01.ranger-bow");
        if (kind == "partingShot")
            strike["at"] = At(Int(target, "x"), Int(target, "y"));
        return kind == "attack" ? MainAttackScope(source, target, "c01.ranger-bow", strike) : [strike];
    }

    private async Task CheckArcherShotBoundaries(BoardView board, JsonObject state, JsonObject source, JsonObject target)
    {
        foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
            foreach (var milliseconds in new[] { 319.999, 320, 320.001, 599.999, 600, 600.001, 739.999, 740, 740.001, 1000 })
            {
                var events = ArcherEvents(kind, source, target);
                var observed = new List<int>();
                void Presented(BattlePresentationTransition _, int index) => observed.Add(index);
                board.EventPresented += Presented;
                try
                {
                    var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
                    await Tick(board, milliseconds / 1000);
                    var sprite = Actor(board, Int(source, "id")).GetNode<Sprite2D>("AuthoredSprite");
                    var expected = assets.ResolveUnitClip("c01.archer", milliseconds < 600 ? "attack" : "idle", ActorClipUnit.Milliseconds);
                    CheckClipSprite(sprite, expected.Asset, expected.FrameAt(milliseconds < 600 ? milliseconds : 0), 1);
                    Require((board.GetNodeOrNull("BattleEffects/Hit") is not null)
                        == (milliseconds >= 320 && milliseconds < 740),
                        "Production release and recovery remain exact under independent before/at/after and overshoot samples.");
                    if (milliseconds >= 740)
                        Require(playing.IsCompleted && await playing && observed.SequenceEqual(Enumerable.Range(0, events.Length)),
                            "One overshooting tick settles all source and recipient ownership with one publication per event.");
                    else
                    {
                        Require(observed.SequenceEqual(kind == "attack" && milliseconds >= 320 ? new[] { 0 } : Array.Empty<int>()),
                            "Only the activation publishes at release, before its impact receipt completes.");
                        board.CancelPresentation();
                        Require(!await playing, "Cancelled boundary samples never complete the receipt.");
                    }
                }
                finally { board.EventPresented -= Presented; }
            }
    }

    private async Task CheckArcherShotScope(BoardView board, JsonObject state, JsonObject source, JsonObject target)
    {
        var aid = Int(source, "id");
        var did = Int(target, "id");
        var events = new[] { new JsonObject { ["type"] = "castResolved", ["unit"] = aid } }
            .Concat(MainAttackScope(source, target, "c01.ranger-bow",
                ReleaseStrike("areaAttack", aid, did, "c01.ranger-bow"),
                ReleaseStrike("areaAttack", aid, did, "c01.ranger-bow"))).ToArray();
        var observed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => observed.Add(index);
        board.EventPresented += Presented;
        var playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), events));
        try
        {
            await Tick(board, .320);
            var first = board.GetNode<Node2D>("BattleEffects/Hit");
            Require(observed.SequenceEqual(new[] { 0, 1 }) && Frame(Actor(board, aid)) == 3,
                "The declaration publishes synchronously and the sole activation releases once at 320 ms.");
            await Tick(board, .420);
            Require(board.GetNodeOrNull("BattleEffects/Hit") is not null
                && !ReferenceEquals(first, board.GetNodeOrNull("BattleEffects/Hit"))
                && Actor(board, aid).GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString()
                    == assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds).Asset.Identity,
                "castResolved adds no source replay and a second area victim starts only another target impact, after idle restoration.");
            await Tick(board, .420);
            Require(playing.IsCompleted && await playing && observed.SequenceEqual(Enumerable.Range(0, events.Length)),
                "Two victims use one 320 ms wind-up and two unchanged 420 ms impacts, publishing each event exactly once.");
        }
        finally { board.EventPresented -= Presented; }
        playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
            [new JsonObject { ["type"] = "castResolved", ["unit"] = aid }]));
        Require(playing.IsCompleted && await playing && board.GetNodeOrNull("BattleEffects") is null,
            "A non-action castResolved declaration is synchronous and cannot start a second animation clock.");
        CheckSettledClipActor(board, aid, assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds).Asset, board.Projection.CellContact(new(12, 16)));
        foreach (var sourceId in new[] { aid, int.MaxValue })
        {
            var visible = new HashSet<int> { did };
            var activation = Activation(sourceId, "c01.ranger-bow");
            playing = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                [activation, ReleaseStrike("attack", sourceId, did, "c01.ranger-bow"), ActivationCompleted(sourceId, "c01.ranger-bow")], visible), visible);
            Require(board.GetNodeOrNull($"CanonicalProjection/Unit_{aid}") is null
                && board.GetNodeOrNull("BattleEffects/Hit") is not null,
                "Hidden or absent production archers introduce no source pose or hidden release delay.");
            await Drain(board, playing);
        }
        var sourceOnly = new HashSet<int> { aid };
        playing = board.PresentTransitionAsync(viewportFeedback,
            Transition(state, state, IdleAction(), ArcherEvents("attack", source, target), sourceOnly), sourceOnly);
        await Tick(board, .320);
        Require(Frame(Actor(board, aid)) == 3 && board.GetNodeOrNull("BattleEffects/Hit") is null,
            "Visible activation keeps its declared release but exposes no feedback for a hidden victim.");
        await Tick(board, .280);
        Require(playing.IsCompleted && await playing,
            "A hidden victim adds no recipient lifetime beyond the visible source recovery.");
    }

    private async Task CheckArcherShotLifetime(BoardView board, JsonObject state, JsonObject source, JsonObject target)
    {
        var aid = Int(source, "id");
        var origin = board.Projection.CellContact(new(12, 16));
        foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
        {
            var events = ArcherEvents(kind, source, target);
            var transition = Transition(state, state, IdleAction(), events);
            var observed = new List<int>();
            void Presented(BattlePresentationTransition _, int index) => observed.Add(index);
            board.EventPresented += Presented;
            try
            {
                foreach (var seconds in new[] { .190, .400, .650 })
                {
                    observed.Clear();
                    var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                    await Tick(board, seconds);
                    board.CancelPresentation();
                    Require(!await playing && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null,
                        "Interruption before release, during authored recovery or during recipient-only feedback cleans both owners.");
                    await Tick(board, 1);
                    Require(observed.SequenceEqual(kind == "attack" && seconds >= .320 ? new[] { 0 } : Array.Empty<int>()),
                        "Retired source and recipient callbacks publish no impact or completion after cancellation.");
                    CheckArcherIdleActor(board, aid, origin, 1000);
                }
                foreach (var exit in new[] { false, true })
                {
                    observed.Clear();
                    var playing = board.PresentTransitionAsync(viewportFeedback, transition);
                    await Tick(board, .400);
                    if (exit)
                    {
                        RemoveChild(board);
                        Require(!await playing, "Tree exit cancels the released shot without another process frame.");
                        AddChild(board);
                    }
                    ConfigureActorClipBoard(board, assets);
                    board.RenderState(state);
                    await Tick(board, 1);
                    Require(!await playing && board.GetNodeOrNull("BattleEffects") is null && !board.IsPresenting
                        && observed.SequenceEqual(kind == "attack" ? new[] { 0 } : Array.Empty<int>()),
                        "Rebinding or reattaching cannot resurrect a retired production shot.");
                    CheckArcherIdleActor(board, aid, origin, 1000);
                }
                board.AnimationsEnabled = false;
                try
                {
                    observed.Clear();
                    var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
                    Require(reduced.IsCompleted && await reduced && board.GetNodeOrNull("BattleEffects") is null
                        && observed.SequenceEqual(Enumerable.Range(0, events.Length)),
                        "Reduced motion settles production shooting synchronously without a source pose or delayed effect.");
                    CheckSettledClipActor(board, aid, assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds).Asset, origin);
                }
                finally { board.AnimationsEnabled = true; }
            }
            finally { board.EventPresented -= Presented; }
        }
    }
}
