using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        internal void RunEffectsVerification(string directory)
        {
            Directory.CreateDirectory(directory);
            timer.Stop();
            using StringWriter errors = new();
            using TextWriterTraceListener listener = new(errors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            try
            {
                BindGame(new GameEngine(2026));
                Lobby.Visibility = Visibility.Collapsed;
                game.Start("guanyu", Role.Lord, GameMode.Identity8);
                game.Advance();
                ClearBattleEffects();
                Capture(Path.Combine(directory, "01-refined-board.png"));
                string saved = game.Save();
                Card card = new(999, CardKind.Dodge, "♥", 10);
                RenderBattleCue(new GameCue(GameCueKind.SkillActivated, "guanyu", Skill: "武圣", Seat: 0));
                RenderBattleCue(new GameCue(GameCueKind.CardPlayed, "guanyu", CardKind.Slash,
                    Seat: 0, TargetSeat: 1, PhysicalCard: card));
                PumpEffects(270);
                if (battleEffects.Count == 0 || EffectsCanvas.IsHitTestVisible)
                {
                    throw new InvalidOperationException("Effects did not animate or intercepted game input.");
                }

                Capture(Path.Combine(directory, "02-skill-and-card.png"));
                ClearBattleEffects();
                RenderBattleCue(new GameCue(GameCueKind.Damaged, "liubei",
                    Seat: 1, SourceSeat: 0, Amount: 2));
                RenderBattleCue(new GameCue(GameCueKind.Healed, "zhouyu", Seat: 3, Amount: 1));
                RenderBattleCue(new GameCue(GameCueKind.CardResponded, "zhaoyun", CardKind.Dodge,
                    Seat: 7, PhysicalCard: card));
                PumpEffects(300);
                Capture(Path.Combine(directory, "03-damage-dodge-heal.png"));
                PumpEffects(1300);
                if (battleEffects.Count != 0 || EffectsCanvas.Children.Count != 0)
                {
                    throw new InvalidOperationException("Completed effect clocks or visuals were not removed.");
                }

                for (int index = 0; index < 40; index++)
                {
                    RenderBattleCue(new GameCue(GameCueKind.Healed, "guanyu", Seat: index % 8, Amount: 1));
                }

                if (battleEffects.Count != 32 || EffectsCanvas.Children.Count != 32)
                {
                    throw new InvalidOperationException("Battle effect visual limit was not enforced.");
                }

                VisualEffects_Click(this, new RoutedEventArgs());
                RenderBattleCue(new GameCue(GameCueKind.Healed, "guanyu", Seat: 0, Amount: 1));
                if (battleEffects.Count != 0 || EffectsCanvas.Children.Count != 0)
                {
                    throw new InvalidOperationException("Disabling effects did not clear and suppress animation.");
                }

                VisualEffects_Click(this, new RoutedEventArgs());
                RenderBattleCue(new GameCue(GameCueKind.Healed, "guanyu", Seat: 0, Amount: 1));
                Pause_Click(this, new RoutedEventArgs());
                if (battleEffects.Count != 0)
                {
                    throw new InvalidOperationException("Pause did not clear battle effects.");
                }

                Pause_Click(this, new RoutedEventArgs());
                Rules_Click(this, new RoutedEventArgs());
                RenderBattleCue(new GameCue(GameCueKind.Healed, "guanyu", Seat: 0, Amount: 1));
                if (battleEffects.Count != 0)
                {
                    throw new InvalidOperationException("An overlay allowed battle effects to continue.");
                }

                CloseRules_Click(this, new RoutedEventArgs());
                RenderBattleCue(new GameCue(GameCueKind.Healed, "guanyu", Seat: 0, Amount: 1));
                if (saved != game.Save())
                {
                    throw new InvalidOperationException("Visual effects changed the game state.");
                }

                BindGame(GameEngine.Load(saved, 2026));
                if (battleEffects.Count != 0)
                {
                    throw new InvalidOperationException("Effects survived a game switch.");
                }

                VerifyNativeKillEffects(directory);
                VerifyNativeBattleStart(directory);
                listener.Flush();
                File.WriteAllText(Path.Combine(directory, "bindings.txt"), errors.ToString());
                if (!string.IsNullOrWhiteSpace(errors.ToString()))
                {
                    throw new InvalidOperationException($"Effect UI binding errors: {errors}");
                }

                File.WriteAllText(Path.Combine(directory, "result.txt"),
                    "PASS: native WPF card-flight, skill banner, attack trace, damage, dodge and healing " +
                    "animations rendered; completed visuals/clocks removed; 32-visual limit, disable, " +
                    "pause and game-switch cleanup verified; no hit testing or rule-state changes; " +
                    "official opening trigger/skip/disable/load cleanup; kill/death gray portraits, official stamps/titles, final-kill survival, skip, seven-kill queue and natural cleanup verified; zero binding errors. Screenshots are native renders, not manual interaction tests.");
            }
            finally
            {
                ClearBattleEffects();
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        }

        private static void PumpEffects(int milliseconds)
        {
            DispatcherFrame frame = new();
            DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => frame.Continue = false;
            try
            {
                timer.Start();
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timer.Stop();
            }
        }
    }
}
