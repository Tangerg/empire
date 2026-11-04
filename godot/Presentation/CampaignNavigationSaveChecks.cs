#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Godot;
using File = System.IO.File;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private async Task CheckMenuSaveAdmission()
    {
        var originalManual = File.ReadAllBytes(manualPath);
        var originalManualStamp = File.GetLastWriteTimeUtc(manualPath);
        var originalAutomatic = File.ReadAllBytes(automaticPath);
        var originalAutomaticStamp = File.GetLastWriteTimeUtc(automaticPath);
        GrayFlagGame? game = null;
        void KeyEvent(Key key)
        {
            foreach (var pressed in new[] { true, false })
                GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed }, true);
        }
        async Task CloseNotices()
        {
            foreach (var notice in game!.GetChildren().OfType<AcceptDialog>().ToArray())
                Press(notice.GetOkButton());
            await Layout();
        }
        async Task RoundTrip(string mode, Type viewType)
        {
            var owner = Campaign(game!);
            var state = State(game!);
            var battle = owner.Battle is { } pending ? Digest(pending.Snapshot()) : null;
            var automatic = File.Exists(automaticPath) ? File.ReadAllBytes(automaticPath) : null;
            var stamp = Stamp(automaticPath);
            KeyEvent(Key.F5);
            Require(EcmaJson.Parse(File.ReadAllText(manualPath))!["mode"]!.GetValue<string>() == mode,
                "An admitted campaign view must retain ordinary F5 and its existing save location.");
            await CloseNotices();
            KeyEvent(Key.F9);
            await Layout();
            Require(!ReferenceEquals(Campaign(game!), owner) && View<Control>(game!).GetType() == viewType
                && State(game!) == state && Campaign(game!).HasPendingBattle == owner.HasPendingBattle
                && Campaign(game!).AtCamp == owner.AtCamp
                && (Campaign(game!).Battle is { } loaded ? Digest(loaded.Snapshot()) : null) == battle,
                "Ordinary active-view F5/F9 must restore the exact campaign, pending battle and location.");
            Require(Stamp(automaticPath) == stamp && (automatic is null ? !File.Exists(automaticPath)
                : File.ReadAllBytes(automaticPath).SequenceEqual(automatic)),
                "A manual active-view round trip cannot rewrite the separate automatic slot.");
            await CloseNotices();
        }
        try
        {
            ResetAutosave();
            game = MountGame();
            Press(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
            game.ProcessMode = ProcessModeEnum.Inherit;
            await Layout();
            // An absent slot prevents an inert story save from passing on an identical old prologue.
            File.Delete(manualPath);
            await RoundTrip("campaign", typeof(StoryScreen));
            while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                CompleteStory(game);
            await RoundTrip("campaign", typeof(StoryScreen));
            Press(Buttons(View<StoryScreen>(game)).First(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)));
            await RoundTrip("campaign", typeof(CampaignStagingScreen));
            Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            var session = Campaign(game).Battle!;
            var screen = View<BattleScreen>(game);
            screen.Board.AnimationsEnabled = false;
            game.ProcessMode = ProcessModeEnum.Inherit;
            await Layout();
            var opening = Digest(session.Snapshot());
            TacticalSceneInputChecks.Move(screen, session);
            var played = Digest(session.Snapshot());
            Require(played != opening && session.Revision == 1 && !screen.IsInputBlocked,
                "Menu save check starts with a real played first battle reached through New, story, choice and staging.");
            KeyEvent(Key.F5);
            var saved = File.ReadAllBytes(manualPath);
            var savedStamp = Stamp(manualPath);
            var automatic = File.ReadAllBytes(automaticPath);
            var automaticStamp = Stamp(automaticPath);
            Require(Digest(EcmaJson.Parse(File.ReadAllText(manualPath))!["battle"]!["state"]!.AsObject()) == played,
                "Ordinary battle F5 must produce the legitimate played manual checkpoint before relaunch.");
            Retire(game);
            game = null;
            await Layout();
            game = MountGame();
            game.ProcessMode = ProcessModeEnum.Inherit;
            await Layout();
            var menu = View<CampaignMenuScreen>(game);
            Require(menu.Presented!.CanContinue && Campaign(game).Battle is null,
                "Relaunch reaches the real title menu, not an admitted battle or campaign view.");
            KeyEvent(Key.F5);
            var preserved = File.ReadAllBytes(manualPath).SequenceEqual(saved) && Stamp(manualPath) == savedStamp;
            var savedMode = EcmaJson.Parse(File.ReadAllText(manualPath))!["mode"]!.GetValue<string>();
            GD.Print($"MENU_SAVE_ADMISSION after native F5: preserved={preserved}; savedMode={savedMode}; view={View<Control>(game).GetType().Name}");
            await CloseNotices();
            KeyEvent(Key.F9);
            await Layout();
            GD.Print($"MENU_SAVE_ADMISSION native F9: view={View<Control>(game).GetType().Name}; battle={Campaign(game).Battle is not null}");
            Require(preserved && View<Control>(game) is BattleScreen && Campaign(game).Battle is { } restored
                && Digest(restored.Snapshot()) == played,
                "Main-menu F5 must not overwrite the played manual checkpoint with an unstarted campaign; menu F9 must still restore it.");
            Require(File.ReadAllBytes(automaticPath).SequenceEqual(automatic) && Stamp(automaticPath) == automaticStamp,
                "Menu save admission and manual restore cannot alter the independent clean autosave.");
            Press(Buttons(View<BattleScreen>(game)).Single(button => button.Text == "战役营地"));
            await Layout();
            CampPrompt(game).EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            await Layout();
            await RoundTrip("camp", typeof(CampaignStagingScreen));
            var campSave = File.ReadAllBytes(manualPath);
            var campStamp = Stamp(manualPath);
            Press(Button(View<CampaignStagingScreen>(game), "CampaignExit"));
            await Layout();
            KeyEvent(Key.F5);
            Require(View<Control>(game) is CampaignMenuScreen && File.ReadAllBytes(manualPath).SequenceEqual(campSave)
                && Stamp(manualPath) == campStamp && !game.GetChildren().OfType<AcceptDialog>().Any(),
                "Returning to title must not save an invisible retained campaign or show a misleading success receipt.");
            KeyEvent(Key.F9);
            await Layout();
            Require(View<Control>(game) is CampaignStagingScreen && Campaign(game).AtCamp,
                "Title-menu F9 still restores the intentional camp save.");
            await CloseNotices();
            Press(Button(View<CampaignStagingScreen>(game), "CampaignExit"));
            await Layout();
            File.Delete(manualPath);
            KeyEvent(Key.F5);
            Require(!File.Exists(manualPath) && View<Control>(game) is CampaignMenuScreen,
                "Title-menu F5 without an existing slot must not manufacture an unstarted manual campaign.");
            Require(File.ReadAllBytes(automaticPath).SequenceEqual(automatic) && Stamp(automaticPath) == automaticStamp,
                "Active camp saves and title admission preserve the original clean automatic boundary.");
        }
        finally
        {
            if (game is not null)
                Retire(game);
            File.WriteAllBytes(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalManualStamp);
            File.WriteAllBytes(automaticPath, originalAutomatic);
            File.SetLastWriteTimeUtc(automaticPath, originalAutomaticStamp);
        }
        await Layout();
    }
}
