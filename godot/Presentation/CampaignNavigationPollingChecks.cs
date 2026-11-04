#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using File = System.IO.File;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private async Task CheckIdlePolling()
    {
        var originalManual = File.ReadAllBytes(manualPath);
        var originalStamp = File.GetLastWriteTimeUtc(manualPath);
        ResetAutosave();
        var game = MountGame();
        try
        {
            // Start from a fresh title, not a synthetic battle snapshot.
            Press(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
            while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                CompleteStory(game);
            Press(Buttons(View<StoryScreen>(game)).First(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)));
            Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            await Layout();
            var session = Campaign(game).Battle!;
            var screen = View<BattleScreen>(game);
            screen.Board.AnimationsEnabled = false;
            var opening = Digest(session.Snapshot());
            CheckIdle("opening");
            TacticalSceneInputChecks.Move(screen, session);
            var played = Digest(session.Snapshot());
            Require(played != opening && session.Revision == 1 && session.CanUndo,
                "Polling must be measured after a real legal player move as well as at the opening.");
            CheckIdle("played");
            Require(session.Undo(session.Revision).Accepted && Digest(session.Snapshot()) == opening && !session.CanUndo,
                "Undo must restore the opening and refresh the same session's status queries.");
            screen.Refresh();
            CheckIdle("undone");
            TacticalSceneInputChecks.Move(screen, session);
            var saved = Digest(session.Snapshot());
            game.ProcessMode = ProcessModeEnum.Inherit;
            void KeyEvent(Key key)
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
            }
            KeyEvent(Key.F5);
            KeyEvent(Key.F9);
            await Layout();
            game.ProcessMode = ProcessModeEnum.Disabled;
            Require(!ReferenceEquals(Campaign(game).Battle, session), "Native load must replace the session rather than reuse its cached projection.");
            session = Campaign(game).Battle!;
            screen = View<BattleScreen>(game);
            Require(session.Revision == 0 && !session.CanUndo && Digest(session.Snapshot()) == saved,
                "Loaded session must keep exact played state, independent revision and the existing empty undo history.");
            CheckIdle("loaded");

            void CheckIdle(string label)
            {
                var before = Digest(session.Snapshot());
                var revision = session.Revision;
                Require(!session.IsComputerTurn && !session.IsConcluded && !screen.IsInputBlocked,
                    "Allocation sampling needs a settled human turn; it must never bypass AI or presentation work.");
                for (var frame = 0; frame < 32; frame++)
                    game._Process(1.0 / 60);
                for (var sample = 0; sample < 3; sample++)
                {
                    const int frames = 600;
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var frame = 0; frame < frames; frame++)
                        game._Process(1.0 / 60);
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    timer.Stop();
                    GD.Print($"IDLE_POLLING {label} sample={sample} frames={frames} allocated={allocated} ms={timer.Elapsed.TotalMilliseconds:F3}");
                    // Bound repeated work, not hardware-dependent wall time. One full first-battle clone exceeds this budget.
                    Require(allocated < 256 * 1024, "Settled application polling must not repeatedly clone the full battle.");
                }
                Require(Digest(session.Snapshot()) == before && session.Revision == revision,
                    "Read-only idle polling cannot change battle facts or revision.");
            }
        }
        finally
        {
            File.WriteAllBytes(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalStamp);
            Retire(game);
        }
    }
}
