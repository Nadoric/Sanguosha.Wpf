using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sanguosha.Core;

namespace Sanguosha.Desktop;

public partial class MainWindow
{
    // Executed only by the existing Windows --verify-effects / --verify-audio switches.
    private void VerifyNativeKillEffects(string directory)
    {
        ClearBattleEffects();
        string saved = game.Save();
        Player victim = game.Players[1];
        bool alive = victim.IsAlive;
        try
        {
            victim.IsAlive = false;
            Refresh();
            PortraitCache.DrainForVerification();
            PlayerView dead = new(victim, game, null);
            if (dead.DeathVisibility != Visibility.Visible || dead.Identity == "身份未知" ||
                dead.Portrait.ImageSource is not BitmapSource gray || gray.Format != PixelFormats.Gray8)
                throw new InvalidOperationException("Dead seats are not gray, stamped and identity-revealed.");
            GameCue cue = new(GameCueKind.Eliminated, victim.Hero.Id, Seat: 1, SourceSeat: 0,
                Elimination: new EliminationInfo(0, 1, 1, 1));
            RenderBattleCue(cue);
            OnEliminationReady(new(cue, 2.45, false));
            PumpEffects(350);
            if (!HasKillEffects || KillEffectsCanvas.Children.Count == 0 || KillEffectsCanvas.IsHitTestVisible)
                throw new InvalidOperationException("Kill effect missing or intercepting game input.");
            Capture(Path.Combine(directory, "04-official-kill-title-and-seal.png"));
            RenderBattleCue(new GameCue(GameCueKind.MatchEnded));
            ResultOverlay.Visibility = Visibility.Visible;
            if (!HasKillEffects || KillEffectsCanvas.Children.Count == 0)
                throw new InvalidOperationException("The final kill was cleared by immediate victory.");
            Capture(Path.Combine(directory, "05-final-kill-above-result.png"));
            SkipKillEffects_Click(this, new RoutedEventArgs());
            ResultOverlay.Visibility = Visibility.Collapsed;
            if (HasKillEffects || KillEffectsCanvas.Children.Count != 0 || KillSkipButton.Visibility != Visibility.Collapsed)
                throw new InvalidOperationException("Skipping a kill left clocks, visuals or queued banners behind.");
            for (int i = 1; i <= 7; i++)
            {
                GameCue multiple = cue with { Seat = i, Elimination = new EliminationInfo(0, i, i, i) };
                RenderBattleCue(multiple); OnEliminationReady(new(multiple, 2.45, false));
            }
            if (pendingKillBanners.Count != 6 || battleEffects.Count > 32)
                throw new InvalidOperationException("Rapid multi-kills are unbounded or banners were dropped.");
            VisualEffects_Click(this, new RoutedEventArgs());
            RenderBattleCue(cue);
            if (HasKillEffects || KillEffectsCanvas.Children.Count != 0)
                throw new InvalidOperationException("Effects-off did not clear and suppress kills.");
            VisualEffects_Click(this, new RoutedEventArgs());
            RenderBattleCue(cue);
            OnEliminationReady(new(cue, 2.45, false));
            PumpEffects(2800);
            if (HasKillEffects || KillEffectsCanvas.Children.Count != 0)
                throw new InvalidOperationException("Natural kill completion leaked a visual or queued callback.");
            RenderBattleCue(cue);
            SkipKillEffects_Click(this, new RoutedEventArgs());
            OnEliminationReady(new(cue, 2.45, false));
            if (HasKillEffects) throw new InvalidOperationException("A delayed audio callback resurrected a skipped banner.");
            foreach (string key in new[] { "start", "kill1", "kill2", "kill3", "kill4", "kill5", "kill6", "kill7" })
            {
                DecodedEffect decoded = OfficialFrameLibrary.Get(key).GetAwaiter().GetResult();
                if (decoded.Frames.Any(frame => !frame.IsFrozen)) throw new InvalidOperationException("Effect frame crossed a worker without freezing.");
            }
            ClearBattleEffects();
        }
        finally
        {
            victim.IsAlive = alive;
            ResultOverlay.Visibility = Visibility.Collapsed;
            ClearBattleEffects();
            Refresh();
        }
        if (saved != game.Save()) throw new InvalidOperationException("Kill presentation altered gameplay/save state.");
    }

    private void VerifyNativeBattleStart(string directory)
    {
        string saved = game.Save();
        try
        {
            BindGame(new GameEngine(2026));
            Lobby.Visibility = Visibility.Collapsed;
            game.Start("liubei", Role.Lord, GameMode.Identity8);
            if (!HasStartEffects || game.Round != 0 || EffectsCanvas.IsHitTestVisible)
                throw new InvalidOperationException("Opening must begin before first turn, without intercepting input.");
            PumpEffects(380);
            Capture(Path.Combine(directory, "06-official-opening.png"));
            game.Advance();
            if (!HasStartEffects || game.Round == 0)
                throw new InvalidOperationException("Opening effect blocked normal game advancement.");
            SkipKillEffects_Click(this, new RoutedEventArgs());
            if (HasStartEffects) throw new InvalidOperationException("Opening click-skip failed.");
            VisualEffects_Click(this, new RoutedEventArgs());
            game.Start("liubei", Role.Lord, GameMode.Identity8);
            if (HasStartEffects) throw new InvalidOperationException("Disabled effects still showed an opening.");
            VisualEffects_Click(this, new RoutedEventArgs());
            game.Start("liubei", Role.Lord, GameMode.Identity8);
            PumpEffects(2900);
            if (HasStartEffects) throw new InvalidOperationException("Opening clock / visual failed to expire.");
        }
        finally
        {
            ClearBattleEffects();
            BindGame(GameEngine.Load(saved, 2026));
            Refresh();
        }
        if (HasStartEffects || game.Save() == "") throw new InvalidOperationException("Loading a save replayed the opening.");
    }

    private static void VerifyKillAudioControls()
    {
        using GameAudio test = new(silent: true);
        test.Settings.Muted = false;
        test.Settings.VoicesEnabled = true;
        test.Settings.EffectsEnabled = true;
        test.Settings.VoicesVolume = 0.7;
        test.Settings.EffectsVolume = 0.5;
        test.ApplySettings();
        int beforeOpening = test.EffectPlayCount;
        test.KillEffectsEnabled = false;
        test.Handle(new GameCue(GameCueKind.MatchStarted));
        if (test.EffectPlayCount != beforeOpening) throw new InvalidOperationException("Visual-effects-off still played the opening sound.");
        test.KillEffectsEnabled = true;
        test.Handle(new GameCue(GameCueKind.MatchStarted));
        if (test.EffectPlayCount != beforeOpening + 1) throw new InvalidOperationException("Opening did not play the original official sound exactly once.");
        test.Settings.EffectsEnabled = false; test.ApplySettings();
        test.Handle(new GameCue(GameCueKind.MatchStarted));
        if (test.EffectPlayCount != beforeOpening + 1) throw new InvalidOperationException("Audio effects-off still played the opening.");
        test.Settings.EffectsEnabled = true; test.ApplySettings();
        List<EliminationPresentation> signals = new();
        test.EliminationReady += signals.Add;
        string[] victims = { "liubei", "guanyu", "zhangfei", "caocao", "simayi", "zhouyu", "sunquan" };
        for (int i = 0; i < victims.Length; i++)
            test.Handle(new GameCue(GameCueKind.Eliminated, victims[i], Seat: i + 1, SourceSeat: 0,
                Elimination: new EliminationInfo(0, i + 1, i + 1, i + 1)));
        if (test.ActiveVoice != "liubei:death" || test.PendingVoiceCount != 13)
            throw new InvalidOperationException("Rapid deaths interrupted the first victim / lost the reserved FIFO sequence.");
        if (signals.Count != 0) throw new InvalidOperationException("Kill banner preceded its victim's death voice.");
        test.Handle(new GameCue(GameCueKind.MatchEnded, HumanWon: true));
        for (int i = 0; i < victims.Length; i++)
        {
            if (test.ActiveVoice != $"{victims[i]}:death") throw new InvalidOperationException("Victim voices were reordered.");
            test.FinishVoiceForVerification();
            if (test.ActiveVoice != $"kill:{i + 1}" || signals.Count != i + 1 || signals[i].Cue.Seat != i + 1 || !signals[i].HasSound)
                throw new InvalidOperationException("Visual/announcement cue was not synchronized after its matching victim voice.");
            test.FinishVoiceForVerification();
        }
        if (test.ActiveVoice != null || test.PendingVoiceCount != 0)
            throw new InvalidOperationException("Finished death sequence did not release the audio channel.");
        test.Handle(new GameCue(GameCueKind.MatchStarted));
        test.KillEffectsEnabled = false;
        test.Handle(new GameCue(GameCueKind.Eliminated, "liubei", Seat: 1, Elimination: new EliminationInfo(0, 1, 1, 1)));
        if (test.ActiveVoice != "liubei:death" || test.PendingVoiceCount != 0)
            throw new InvalidOperationException("Effects-off must suppress kill callouts, not victim voices.");
        test.KillEffectsEnabled = true;
        test.Handle(new GameCue(GameCueKind.MatchStarted));
        test.Settings.VoicesEnabled = false; test.ApplySettings();
        test.Handle(new GameCue(GameCueKind.Eliminated, "guanyu", Seat: 1, Elimination: new EliminationInfo(0, 1, 1, 1)));
        if (test.ActiveVoice != "kill:1") throw new InvalidOperationException("Kill callout incorrectly uses hero-voice setting instead of effects setting.");
        test.Settings.EffectsEnabled = false; test.ApplySettings();
        if (test.ActiveVoice != null) throw new InvalidOperationException("Effects-off did not stop the active kill callout.");
        test.Settings.EffectsEnabled = true; test.Settings.VoicesEnabled = true; test.ApplySettings();
        test.Handle(new GameCue(GameCueKind.MatchStarted));
        test.Handle(new GameCue(GameCueKind.Eliminated, "guanyu", Seat: 1, Elimination: new EliminationInfo(0, 1, 1, 1)));
        test.StopKillAnnouncements();
        if (test.ActiveVoice != "guanyu:death" || test.PendingVoiceCount != 0)
            throw new InvalidOperationException("Skip should preserve the victim voice but drop its kill callout.");
        test.Settings.Muted = true; test.ApplySettings();
        if (test.ActiveVoice != null || test.PendingVoiceCount != 0) throw new InvalidOperationException("Mute failed during a kill sequence.");
    }
}
