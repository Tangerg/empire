#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Lifecycle;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckTorrenCelebrationPlayback()
    {
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        var clip = assets.ResolveUnitClip("c01.swordsman", "celebrate", ActorClipUnit.Milliseconds, "campaign-torren");
        var idle = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var starts = new[] { 0.0, 100, 220, 380, 620, 760 };
        var names = new[] { "salute-ready", "salute-lift", "salute-high", "salute-hold", "salute-lower", "salute-rest" };
        Require(bindings.Actors["campaign-torren"].UnitArt.ActionClips["celebrate"] == "torren.salute"
            && bindings.Actors["campaign-torren"].UnitArt.ActionClips["idle"] == "torren.breathe"
            && clip.Asset.Identity == "torren-salute-v1" && clip.Asset.FrameOrder.SequenceEqual(names)
            && clip.FrameIndices.SequenceEqual(Enumerable.Range(0, 6)) && clip.Length == 900
            && clip.ReleaseMs is null && clip.Death is null && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .09f),
            "Torren selects six genuine salute poses for 900 ms with uniform scale and no attack release or death policy.");
        var pictures = new HashSet<string>();
        for (var index = 0; index < starts.Length; index++)
        {
            Require(clip.FrameAt(starts[index]) == index
                && clip.FrameAt(index == 5 ? 899.999 : starts[index + 1] - .001) == index,
                "The production salute preserves all six exact half-open pose intervals.");
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(used.HasArea() && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Every salute picture retains its entire padded sword, head, cloak and feet silhouette.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(900) == 5 && clip.FrameAt(10000) == 5,
            "Six distinct celebration pictures hold their final pose without looping within the finite clip.");

        // Production art on detached receipt data tests presentation, not a lawful campaign victory.
        var state = CelebrationState();
        state["units"] = new JsonArray(state["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
        state["units"]![0]!["facing"] = "east";
        var id = Int(state["units"]![1]!.AsObject(), "id");
        var board = NewActorClipBoard(assets);
        try
        {
            await CheckTorrenCelebrationBoundaries(board, state, id, clip, idle);
            await CheckTorrenCelebrationColor(board, state, id, clip);
            await CheckTorrenCelebrationEligibility(board, state, id, idle);
            await CheckTorrenCelebrationLifetime(board, state, id, idle);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Torren salute: six production poses, 900 ms gate, winner eligibility, mirrors/contact, idle return and lifecycle boundaries passed.");
    }

    private async Task CheckTorrenCelebrationBoundaries(BoardView board, JsonObject state, int id, ActorClip clip, ActorClip idle)
    {
        var generic = Int(state["units"]![0]!.AsObject(), "id");
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var facing in new[] { "west", "east" })
            {
                state["units"]![1]!["facing"] = facing;
                var sign = facing == "west" ? -1 : 1;
                foreach (var ms in new[] { 0, 99.999, 100, 219.999, 220, 379.999, 380, 619.999, 620, 759.999, 760, 899.999, 900, 10000 })
                {
                    published.Clear();
                    var before = (JsonObject)state.DeepClone();
                    before["units"]![1]!["owner"] = 2;
                    var bytes = before.ToJsonString() + state.ToJsonString();
                    var task = board.PresentTransitionAsync(viewportFeedback,
                        Transition(before, state, IdleAction(), [CelebrationEvent()]));
                    var actor = Actor(board, id);
                    var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                    var origin = actor.Position;
                    var contact = actor.GetMeta("logical_contact").AsVector2();
                    await Tick(board, ms / 1000);
                    if (ms < 900)
                    {
                        var frame = ms < 100 ? 0 : ms < 220 ? 1 : ms < 380 ? 2 : ms < 620 ? 3 : ms < 760 ? 4 : 5;
                        CheckClipSprite(sprite, clip.Asset, frame, sign);
                        CheckClipShadow(sprite);
                        Require(!task.IsCompleted && board.IsPresenting && published.Count == 0
                            && actor.Position == origin && actor.GetMeta("logical_contact").AsVector2() == contact
                            && actor.Scale == Vector2.One && actor.Rotation == 0 && sprite.Rotation == 0
                            && actor.SelfModulate == Colors.White && actor.GetNodeOrNull("ActionAccent") is null,
                            $"Torren's committed winning ownership selects a stationary full-opacity salute and holds the result at {ms} ms.");
                        if (ms >= 220)
                            CheckClipSprite(Actor(board, generic).GetNode<Sprite2D>("AuthoredSprite"),
                                assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds).Asset, 0, 1);
                        board.CancelPresentation();
                        Require(!await task && published.Count == 0, "An unfinished salute cannot publish gameOver on cancellation.");
                    }
                    else
                    {
                        Require(task.IsCompleted && await task && !board.IsPresenting && published.SequenceEqual(new[] { 0 }),
                            "The cohort releases gameOver exactly at 900 ms, never 220 ms, actor-duration sum or 1120 ms.");
                        CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, sign);
                        await Tick(board, .440);
                        CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 1, sign);
                    }
                    Require(before.ToJsonString() + state.ToJsonString() == bytes
                        && board.GetNodeOrNull("BattleEffects") is null && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                        "Production celebration preserves snapshots and releases both transient layers.");
                }
            }
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckTorrenCelebrationColor(BoardView board, JsonObject state, int id, ActorClip clip)
    {
        var tint = new Color(.2f, .4f, .7f, .6f);
        var scale = new Vector2(.8f, 1.2f);
        var origin = Vector2.Zero;
        void Initialize(BattlePresentationTransition _, int index)
        {
            if (index != 0)
                return;
            var actor = Actor(board, id);
            actor.SelfModulate = tint;
            actor.Scale = scale;
            actor.Position += new Vector2(3, -4);
            origin = actor.Position;
        }
        board.EventPresented += Initialize;
        try
        {
            foreach (var ms in new[] { 0, 100, 220, 380, 620, 760, 899 })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(),
                    [new JsonObject { ["type"] = "facingChanged", ["unit"] = id, ["to"] = "east" }, CelebrationEvent()]));
                var actor = Actor(board, id);
                var contact = actor.GetMeta("logical_contact").AsVector2();
                await Tick(board, ms / 1000.0);
                Require(!task.IsCompleted && actor.SelfModulate.IsEqualApprox(tint)
                    && actor.Position == origin && actor.Scale == scale && actor.Rotation == 0
                    && actor.GetMeta("logical_contact").AsVector2() == contact,
                    "Every production salute pose preserves captured RGBA, root transform and logical contact.");
                CheckClipSprite(actor.GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, clip.FrameAt(ms), 1);
                board.CancelPresentation();
                Require(!await task, "A tinted production salute remains cancellable.");
            }
        }
        finally { board.EventPresented -= Initialize; }
    }

    private async Task CheckTorrenCelebrationEligibility(BoardView board, JsonObject state, int id, ActorClip idle)
    {
        var generic = Int(state["units"]![0]!.AsObject(), "id");
        foreach (var exclusion in new[] { "loser", "dead", "absent", "before-hidden", "after-hidden", "undrawn" })
        {
            var manifest = PresentationJson.Load<AssetManifest>("res://data/selected-assets.json");
            var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
            bindings.Actors["campaign-torren"].UnitArt.ActionClips.Remove("celebrate");
            ConfigureActorClipBoard(board, new AssetCatalog(manifest, bindings));
            var before = (JsonObject)state.DeepClone();
            var after = (JsonObject)state.DeepClone();
            var beforeVisible = new HashSet<int> { generic, id };
            var afterVisible = new HashSet<int> { generic, id };
            switch (exclusion)
            {
                case "loser":
                    after["units"]![1]!["owner"] = 2;
                    break;
                case "dead":
                    after["units"]![1]!["hp"] = 0;
                    break;
                case "absent":
                    after["units"]!.AsArray().RemoveAt(1);
                    break;
                case "before-hidden":
                    beforeVisible.Remove(id);
                    break;
                case "after-hidden":
                    afterVisible.Remove(id);
                    break;
                case "undrawn":
                    before["units"]!.AsArray().RemoveAt(1);
                    break;
            }
            var task = board.PresentTransitionAsync(viewportFeedback,
                Transition(before, after, IdleAction(), [CelebrationEvent()], beforeVisible), afterVisible);
            await Tick(board, .220);
            Require(task.IsCompleted && await task && !board.IsPresenting,
                "An excluded named Torren never resolves missing salute art or extends the remaining static winner's 220 ms beat.");
        }
        var rejected = false;
        try
        {
            await board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [CelebrationEvent()]));
        }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && !board.IsPresenting && board.GetNodeOrNull("BattleEffects") is null
            && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
            "Visible winning Torren with missing salute art fails explicitly and releases ownership without fallback.");
        CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
        ConfigureActorClipBoard(board, assets);
        foreach (var conclusion in new[] { BattleConclusion.LastTeamRemaining(2), BattleConclusion.AllDefeated(),
            BattleConclusion.TurnLimitReached(), BattleConclusion.NoActors() })
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [CelebrationEvent(conclusion)]));
            await Tick(board, .219999);
            Require(!task.IsCompleted, "Defeat and draw preserve the minimal result beat without beginning Torren's salute.");
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
            await Tick(board, .000001);
            Require(task.IsCompleted && await task, "No winning Torren means no 900 ms salute delay.");
        }
        var terminal = (JsonObject)state.DeepClone();
        terminal["phase"] = "over";
        terminal["conclusion"] = BattleConclusion.LastTeamRemaining(CelebrationTeam).ToJson();
        var noEvent = board.PresentTransitionAsync(viewportFeedback, Transition(terminal, terminal, IdleAction(), Array.Empty<JsonObject>()));
        Require(noEvent.IsCompleted && await noEvent, "A winning terminal snapshot cannot originate celebration without gameOver.");
        CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
    }

    private async Task CheckTorrenCelebrationLifetime(BoardView board, JsonObject state, int id, ActorClip idle)
    {
        var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(value => Int(value, "id") == id);
        var contact = board.Projection.CellContact(new(Int(unit, "x"), Int(unit, "y")));
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var ms in new[] { 100, 380, 760 })
            {
                foreach (var interruption in new[] { "cancel", "rebind", "tree-exit" })
                {
                    var task = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [CelebrationEvent()]));
                    await Tick(board, ms / 1000.0);
                    if (interruption == "cancel")
                        board.CancelPresentation();
                    else if (interruption == "rebind")
                    {
                        ConfigureActorClipBoard(board, assets);
                        board.RenderState(state);
                    }
                    else
                        RemoveChild(board);
                    Require(!await task && published.Count == 0 && !board.IsPresenting,
                        "Cancel, rebind and tree exit retire the production salute without publishing its unfinished gameOver.");
                    if (interruption == "tree-exit")
                    {
                        AddChild(board);
                        ConfigureActorClipBoard(board, assets);
                        board.RenderState(state);
                    }
                    CheckSettledClipActor(board, id, idle.Asset, contact);
                    await Tick(board, 1.8);
                    CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
                    Require(published.Count == 0 && board.GetNodeOrNull("BattleEffects") is null
                        && viewportFeedback.GetNodeOrNull("ViewportEffects") is null,
                        "Retired salute callbacks cannot resurrect finite poses or publish after settlement.");
                }
            }
            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, Transition(state, state, IdleAction(), [CelebrationEvent()]));
            Require(reduced.IsCompleted && await reduced && published.SequenceEqual(new[] { 0 }) && !board.IsPresenting,
                "Reduced motion settles the genuine production salute synchronously with one ordered gameOver publication.");
            CheckSettledClipActor(board, id, idle.Asset, contact);
        }
        finally { board.AnimationsEnabled = true; board.EventPresented -= Presented; }
    }
}
