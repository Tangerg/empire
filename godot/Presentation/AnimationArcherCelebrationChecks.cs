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
    private async Task CheckArcherCelebrationPlayback()
    {
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        var clip = assets.ResolveUnitClip("c01.archer", "celebrate", ActorClipUnit.Milliseconds);
        var idle = assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds);
        var names = new[] { "cheer-ready", "cheer-lift", "cheer-high", "cheer-hold", "cheer-lower", "cheer-rest" };
        var expected = new AuthoredCelebrationExpectation("c01.archer", null,
            new[] { 0.0, 100, 220, 380, 640, 820 }, 960, 500, 2200);
        Require(bindings.Units["c01.archer"].ActionClips["celebrate"] == "archer.cheer"
            && bindings.Units["c01.archer"].ActionClips["idle"] == "archer.breathe"
            && bindings.Units["c01.archer"].AppearanceId is null
            && clip.Asset.Identity == "archer-cheer-v1" && clip.Asset.TopicId == "C01-UNIT-ARCHER"
            && clip.Asset.FrameCount == 6 && clip.Asset.FrameOrder.SequenceEqual(names)
            && clip.FrameIndices.SequenceEqual(Enumerable.Range(0, 6)) && clip.Length == 960
            && clip.ReleaseMs is null && clip.Death is null && Mathf.IsEqualApprox(clip.Asset.PresentationScale, .0805f),
            "Shared C01 archers select six genuine cheering poses for 960 ms, without changing base appearance or declaring attack/death policy.");
        Require(!bindings.Actors.ContainsKey("campaign-elin")
            && ReferenceEquals(assets.ResolveUnitClip("c01.archer", "celebrate", ActorClipUnit.Milliseconds, "campaign-elin"), clip)
            && assets.ResolveUnit("c01.archer", "campaign-elin") == assets.ResolveUnit("c01.archer"),
            "Elin inherits the shared archer cheer without a named actor override.");
        var pictures = new HashSet<string>();
        for (var index = 0; index < expected.Starts.Length; index++)
        {
            Require(clip.FrameAt(expected.Starts[index]) == index
                && clip.FrameAt((index == 5 ? 960 : expected.Starts[index + 1]) - .001) == index,
                "Archer celebration preserves exact half-open 100/120/160/260/180/140 ms pose intervals.");
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Every archer cheer picture retains its full RGBA silhouette and transparent padding.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(960) == 5 && clip.FrameAt(10000) == 5,
            "Six distinct cheering pictures hold the final pose without looping within the finite clip.");
        foreach (var (action, binding) in new[] { ("idle", "archer.breathe"), ("move", "legacy.move"),
            ("attack", "archer.loose"), ("hit", "archer.recoil"), ("death", "archer.fall") })
            Require(bindings.Units["c01.archer"].ActionClips[action] == binding,
                "Archer celebration preserves every other shared action binding.");

        // Production art on detached receipt data tests presentation, not a lawful campaign victory.
        var state = CelebrationState();
        state["units"] = new JsonArray(state["units"]!.AsArray().Take(2).Select(unit => unit!.DeepClone()).ToArray());
        state["units"]![0]!["facing"] = "east";
        var unit = state["units"]![1]!.AsObject();
        unit["type"] = "c01.archer";
        var id = Int(unit, "id");
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var key in new string?[] { null, "campaign-elin" })
            {
                if (key is null)
                    unit.Remove("key");
                else
                    unit["key"] = key;
                await CheckAuthoredCelebrationBoundaries(board, state, id, clip, idle, expected);
                await CheckAuthoredCelebrationColor(board, state, id, clip, expected);
                await CheckAuthoredCelebrationEligibility(board, state, id, idle, expected);
                await CheckAuthoredCelebrationLifetime(board, state, id, idle, expected);
            }
            await CheckArcherCelebrationCohort(board, state, id, clip, idle);
            await CheckArcherCelebrationAfterDeath(board, state, id, clip, idle);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Archer cheer: six production poses, shared Elin binding, 960 ms cohort gate, death ordering, mirrors/contact, idle return and lifecycle boundaries passed.");
    }

    private async Task CheckArcherCelebrationCohort(BoardView board, JsonObject state, int id, ActorClip clip, ActorClip idle)
    {
        var pair = (JsonObject)state.DeepClone();
        pair["units"]![0]!["key"] = "campaign-torren";
        var torren = Int(pair["units"]![0]!.AsObject(), "id");
        var torrenClip = assets.ResolveUnitClip("c01.swordsman", "celebrate", ActorClipUnit.Milliseconds, "campaign-torren");
        var torrenIdle = assets.ResolveUnitClip("c01.swordsman", "idle", ActorClipUnit.Milliseconds, "campaign-torren");
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var bytes = pair.ToJsonString();
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(pair, pair, IdleAction(), [CelebrationEvent()]));
            CheckClipSprite(Actor(board, torren).GetNode<Sprite2D>("AuthoredSprite"), torrenClip.Asset, 0, 1);
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, 0, 1);
            await Tick(board, .899999);
            Require(!task.IsCompleted && published.Count == 0, "Both authored winners hold the receipt through Torren's last salute interval.");
            CheckClipSprite(Actor(board, torren).GetNode<Sprite2D>("AuthoredSprite"), torrenClip.Asset, 5, 1);
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, 5, 1);
            await Tick(board, .000001);
            CheckClipSprite(Actor(board, torren).GetNode<Sprite2D>("AuthoredSprite"), torrenIdle.Asset, 0, 1);
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, 5, 1);
            Require(!task.IsCompleted && published.Count == 0,
                "Torren resumes breathing at 900 ms while the archer independently retains the result gate.");
            await Tick(board, .059999);
            Require(!task.IsCompleted && published.Count == 0, "The archer's last half-open interval cannot release gameOver early.");
            await Tick(board, .000001);
            Require(task.IsCompleted && await task && !board.IsPresenting && published.SequenceEqual(new[] { 0 }),
                "Mixed authored winners release gameOver once at 960 ms, not 900, 1180 or the 1860 ms duration sum.");
            CheckClipSprite(Actor(board, torren).GetNode<Sprite2D>("AuthoredSprite"), torrenIdle.Asset, 0, 1);
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
            await Tick(board, .500);
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 1, 1);
            Require(pair.ToJsonString() == bytes && published.Count == 1,
                "Mixed celebration preserves the committed state and cannot republish its completed event.");
        }
        finally { board.EventPresented -= Presented; }
    }

    private async Task CheckArcherCelebrationAfterDeath(BoardView board, JsonObject state, int id, ActorClip clip, ActorClip idle)
    {
        var before = (JsonObject)state.DeepClone();
        var dying = before["units"]![0]!.AsObject();
        dying["type"] = "c01.archer";
        dying["owner"] = 2;
        var dead = Int(dying, "id");
        var after = WithoutDeathActor(before, dead);
        var bytes = before.ToJsonString() + after.ToJsonString();
        var fall = assets.ResolveUnitClip("c01.archer", "death", ActorClipUnit.Milliseconds);
        var published = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => published.Add(index);
        board.EventPresented += Presented;
        try
        {
            var task = board.PresentTransitionAsync(viewportFeedback, Transition(before, after, IdleAction(),
                [ReactionDeathEvent(dead), CelebrationEvent()]));
            await Tick(board, .620);
            Require(!task.IsCompleted && published.Count == 0 && Actor(board, dead).SelfModulate.A == 1,
                "The last loser's full-opacity fall completes before its terminal fade, without starting victory.");
            CheckClipSprite(Actor(board, dead).GetNode<Sprite2D>("AuthoredSprite"), fall.Asset, 5, 1);
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 1, 1);
            await Tick(board, .159);
            Require(!task.IsCompleted && published.Count == 0 && Actor(board, dead).SelfModulate.A > 0,
                "The winning archer cannot begin celebration before the last death's complete 160 ms fade.");
            await Tick(board, .001);
            Require(!task.IsCompleted && published.SequenceEqual(new[] { 0 })
                && board.GetNodeOrNull($"CanonicalProjection/Unit_{dead}") is null,
                "Only retirement at 780 ms publishes death and starts the winning archer's finite cheer.");
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), clip.Asset, 0, 1);
            await Tick(board, .220);
            Require(!task.IsCompleted && board.IsPresenting && published.SequenceEqual(new[] { 0 }),
                "The old result beat cannot bypass the authored cheer after the last death.");
            await Tick(board, .739999);
            Require(!task.IsCompleted && published.SequenceEqual(new[] { 0 }),
                "The result remains held until all 960 ms of the post-death celebration finish.");
            await Tick(board, .000001);
            Require(task.IsCompleted && await task && !board.IsPresenting && published.SequenceEqual(new[] { 0, 1 })
                && before.ToJsonString() + after.ToJsonString() == bytes,
                "Death then gameOver publish once after 620+160+960 ms without mutating the committed receipt.");
            CheckClipSprite(Actor(board, id).GetNode<Sprite2D>("AuthoredSprite"), idle.Asset, 0, 1);
        }
        finally { board.EventPresented -= Presented; }
    }
}
