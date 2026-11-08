using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        internal void RunDialogVerification(string directory)
        {
            Directory.CreateDirectory(directory);
            timer.Stop();
            using StringWriter errors = new();
            using TextWriterTraceListener listener = new(errors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            try
            {
                VerifyTargetCardUi(directory);
                StartDialogScenario("sunquan");
                if (!game.UseSkill(null, null) || game.PendingDecision == null ||
                    !game.PendingDecision.Options.Any(option => option.PreviewCard != null))
                {
                    throw new InvalidOperationException("Zhiheng did not present explicit own-card previews.");
                }

                if (game.PendingDecision.Options.Any(option => option.PreviewCard is Card card &&
                    !game.Human.Hand.Concat(game.Human.Equipment).Contains(card)))
                {
                    throw new InvalidOperationException("Own-card picker exposed an unrelated card.");
                }

                Capture(Path.Combine(directory, "11-skill-cards.png"));
                Button original = DialogButtons().First(button => button.Tag is string key &&
                    game.PendingDecision.Options.Any(option => option.Key == key && option.PreviewCard != null));
                original.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                SkillDecisionRequest next = game.PendingDecision!;
                original.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!ReferenceEquals(next, game.PendingDecision))
                {
                    throw new InvalidOperationException("An old dialog click resolved a later request.");
                }

                Capture(Path.Combine(directory, "12-skill-progress.png"));
                DialogButtons().Single(button => Equals(button.Tag, "done"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (game.PendingDecision != null || game.Phase != GamePhase.Play)
                {
                    throw new InvalidOperationException("Skill completion footer did not finish selection.");
                }

                StartDialogScenario("zhenji");
                if (game.PendingDecision == null || !DialogButtons().Any(button => Equals(button.Tag, "no")))
                {
                    throw new InvalidOperationException("Optional skill did not expose its decline action.");
                }

                Capture(Path.Combine(directory, "13-skill-invoke.png"));
                DialogButtons().Single(button => Equals(button.Tag, "no"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                StartDialogScenario("zhangliao");
                if (game.PendingDecision == null ||
                    !game.PendingDecision.Options.Any(option => option.PreviewHeroId != null) ||
                    game.PendingDecision.Options.Any(option => option.PreviewCard != null))
                {
                    throw new InvalidOperationException("Target seats were confused with physical card IDs.");
                }

                Capture(Path.Combine(directory, "14-skill-targets.png"));

                StartDialogScenario("zhugeliang");
                if (game.PendingDecision == null || game.PendingDecision.Options.Any(option =>
                    option.PreviewCard == null || !option.Key.StartsWith("top:") &&
                    !option.Key.StartsWith("bottom:")))
                {
                    throw new InvalidOperationException("Guanxing lost its visible cards or top/bottom actions.");
                }

                Capture(Path.Combine(directory, "15-guanxing.png"));

                StartDialogScenario("taishici");
                if (!game.UseSkill(null, 1) || game.PendingDecision == null ||
                    game.PendingDecision.Options.Any(option => option.PreviewCard == null ||
                        !game.Human.Hand.Contains(option.PreviewCard)))
                {
                    throw new InvalidOperationException("Pindian must show only the chooser's own cards.");
                }

                Capture(Path.Combine(directory, "16-pindian.png"));

                StartDialogScenario("sunquan");
                SavedGame snapshot = JsonSerializer.Deserialize<SavedGame>(game.Save())!;
                Card[] extra = snapshot.Deck.TakeLast(28).ToArray();
                foreach (Card card in extra)
                {
                    snapshot.Deck.Remove(card);
                    snapshot.Players[0].Hand.Add(card);
                }

                BindGame(GameEngine.Load(JsonSerializer.Serialize(snapshot), 42));
                Refresh();
                game.UseSkill(null, null);
                Capture(Path.Combine(directory, "17-many-cards.png"));
                CardChoiceScroll.ScrollToBottom();
                Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Capture(Path.Combine(directory, "18-many-cards-bottom.png"));
                if (CardChoiceWindow.ActualHeight > 780 || CardChoiceScroll.ScrollableHeight <= 0 ||
                    CardChoiceScroll.VerticalOffset <= 0 ||
                    KeyboardNavigation.GetTabNavigation(CardChoiceOverlay) != KeyboardNavigationMode.Cycle)
                {
                    throw new InvalidOperationException("Long choices overflowed or lost scrolling/focus containment.");
                }

                listener.Flush();
                File.WriteAllText(Path.Combine(directory, "bindings.txt"), errors.ToString());
                if (!string.IsNullOrWhiteSpace(errors.ToString()))
                {
                    throw new InvalidOperationException($"Dialog binding errors: {errors}");
                }

                File.WriteAllText(Path.Combine(directory, "result.txt"),
                    "PASS: official window/card-back textures; hidden hand slots, public equipment/judgments, " +
                    "own-card and target previews, optional skill footer, Guanxing, private Pindian, " +
                    "multi-step completion, stale-click rejection, long-list scrolling and keyboard " +
                    "focus containment verified; zero binding errors. Native WPF renders, not manual input tests.");
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        }

        private void StartDialogScenario(string heroId)
        {
            BindGame(new GameEngine(42));
            Lobby.Visibility = Visibility.Collapsed;
            ResultOverlay.Visibility = Visibility.Collapsed;
            targets.Clear();
            game.Start(heroId);
            for (int step = 0; step < 100 && game.Phase == GamePhase.Resolving; step++)
            {
                game.Advance();
            }

            ClearBattleEffects();
            Refresh();
        }
    }
}
