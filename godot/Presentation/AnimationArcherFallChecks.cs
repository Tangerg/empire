#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckArcherFallPlayback()
    {
        var clip = assets.ResolveUnitClip("c01.archer", "death", ActorClipUnit.Milliseconds);
        var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
        var names = new[] { "fall-buckle", "fall-kneel", "fall-collapse", "fall-contact", "fall-settle", "fall-still" };
        Require(bindings.Units.Count + bindings.Actors.Count == 40
            && bindings.Units["c01.archer"].ActionClips["death"] == "archer.fall"
            && clip.Asset.Identity == "archer-fall-v1" && clip.Asset.TopicId == "C01-UNIT-ARCHER"
            && clip.Asset.FrameCount == 6 && clip.Asset.FrameOrder.SequenceEqual(names)
            && clip.FrameIndices.SequenceEqual(Enumerable.Range(0, 6)) && clip.Length == 620 && clip.ReleaseMs is null
            && clip.RequireDeath().Mode == ActorDeathMode.AfterClip && clip.RequireDeath().TerminalFadeMs == 160,
            "The C01 archer selects six authored falling poses over 620 ms followed by an explicit 160 ms fade.");
        Require(!bindings.Actors.ContainsKey("campaign-elin") && bindings.Units["c01.archer"].AppearanceId is null
            && ReferenceEquals(assets.ResolveUnitClip("c01.archer", "death", ActorClipUnit.Milliseconds, "campaign-elin"), clip)
            && assets.ResolveUnit("c01.archer", "campaign-elin") == assets.ResolveUnit("c01.archer"),
            "Elin inherits the shared C01 archer fall without an actor override or replacement base appearance.");
        foreach (var (action, expected) in new[] { ("idle", "archer.breathe"), ("move", "legacy.move"),
            ("attack", "archer.loose"), ("hit", "archer.recoil"), ("celebrate", "archer.cheer") })
            Require(bindings.Units["c01.archer"].ActionClips[action] == expected,
                "Archer fall changes no non-death action binding.");
        foreach (var type in new[] { "archer", "c01.bran" })
        {
            var other = assets.ResolveUnitClip(type, "death", ActorClipUnit.Milliseconds);
            Require(bindings.Units[type].ActionClips["death"] == "static.death"
                && other.Asset == assets.ResolveUnit(type) && other.Length == 220
                && other.RequireDeath().Mode == ActorDeathMode.DuringClip,
                "The base archer and Bran retain their static death artwork and policy.");
        }
        var starts = new[] { 0.0, 60, 130, 220, 330, 440 };
        var pictures = new HashSet<string>();
        for (var index = 0; index < starts.Length; index++)
        {
            Require(clip.FrameAt(starts[index]) == index
                && clip.FrameAt((index == 5 ? 620 : starts[index + 1]) - .001) == index,
                "Each fall pose owns its exact half-open 60/70/90/110/110/180 ms interval.");
            using var image = assets.CreateFrameTexture(clip.Asset, index).GetImage();
            var used = image.GetUsedRect();
            Require(image.GetFormat() == Image.Format.Rgba8 && used.HasArea()
                && used.Position.X >= 4 && used.Position.Y >= 4
                && used.End.X <= image.GetWidth() - 4 && used.End.Y <= image.GetHeight() - 4,
                "Each archer falling drawing retains its complete RGBA silhouette and transparent padding.");
            pictures.Add(Convert.ToBase64String(image.GetData()));
        }
        Require(pictures.Count == 6 && clip.FrameAt(620) == 5 && clip.FrameAt(10000) == 5,
            "Six distinct fall pictures hold the terminal ground pose without looping or restoring idle.");

        // These production-art receipts test presentation ownership, not lawful battle commands.
        var state = ReleaseState();
        state["units"] = new JsonArray(state["units"]!.AsArray().Take(2)
            .Select(unit => unit!.DeepClone()).ToArray());
        var unit = state["units"]![0]!.AsObject();
        unit["type"] = "c01.archer";
        var id = Int(unit, "id");
        var idle = assets.ResolveUnitClip("c01.archer", "idle", ActorClipUnit.Milliseconds);
        var board = NewActorClipBoard(assets);
        try
        {
            foreach (var key in new string?[] { null, "campaign-elin" })
            {
                if (key is null)
                    unit.Remove("key");
                else
                    unit["key"] = key;
                foreach (var facing in new[] { "west", "east" })
                {
                    unit["facing"] = facing;
                    await CheckAuthoredFallBoundaries(board, state, id, clip, facing == "west" ? -1 : 1);
                }
            }
            await CheckAuthoredFallColor(board, state, id, clip);
            await CheckAuthoredFallScope(board, state, id, idle,
                assets.ResolveUnitClip("c01.archer", "hit", ActorClipUnit.Milliseconds));
            await CheckAuthoredFallResult(board, state, id);
            await CheckAuthoredFallLifetime(board, state, id, idle);
            await CheckAuthoredFallVisibility(board, state, id, "c01.archer", null);
        }
        finally { RemoveChild(board); board.Free(); }
        GD.Print("Archer fall: six padded production poses, shared Elin binding, 620+160 ms phases, mirrors/contact, retirement/result wait and lifecycle boundaries passed.");
    }
}
