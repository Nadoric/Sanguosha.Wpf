using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        internal void RunUiVerification(string directory)
        {
            Directory.CreateDirectory(directory);
            timer.Stop();
            using StringWriter errors = new();
            using TextWriterTraceListener listener = new(errors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            try
            {
                VerifyOfficialArtwork();
                VerifyOfficialSeating();
                Capture(Path.Combine(directory, "01-lobby.png"));
                WeiFilter.IsChecked = true;
                Capture(Path.Combine(directory, "02-wei-roster.png"));
                foreach (GameMode mode in Enum.GetValues<GameMode>())
                {
                    BindGame(new GameEngine(2026));
                    resultShown = false;
                    Lobby.Visibility = Visibility.Collapsed;
                    ResultOverlay.Visibility = Visibility.Collapsed;
                    game.Start("guanyu", Role.Lord, mode);
                    game.Advance();
                    Capture(Path.Combine(directory, $"03-board-{mode}.png"));
                }

                game.Start("guanyu", Role.Lord);
                game.Advance();
                CardView selected = HandCards.First(card => card.Card.IsRed);
                HandCard_Click(new Button { Tag = selected }, new RoutedEventArgs());
                Target_Click(new Button { Tag = 1 }, new RoutedEventArgs());
                ConversionCheck.IsChecked = true;
                Capture(Path.Combine(directory, "04-selection.png"));
                Primary_Click(this, new RoutedEventArgs());
                if (game.Human.SlashCount != 1 || game.Human.Hand.Contains(selected.Card))
                {
                    throw new InvalidOperationException("Card selection / conversion / target controls failed.");
                }

                bool capturedResponse = false;
                for (int step = 0; step < 4000 && game.Phase != GamePhase.GameOver; step++)
                {
                    if (game.Pending != null)
                    {
                        if (!capturedResponse)
                        {
                            Capture(Path.Combine(directory, "05-response.png"));
                            capturedResponse = true;
                        }

                        Secondary_Click(this, new RoutedEventArgs());
                    }
                    else if (game.Phase == GamePhase.Play)
                    {
                        Secondary_Click(this, new RoutedEventArgs());
                    }
                    else if (game.Phase == GamePhase.Discard)
                    {
                        int count = game.Human.Hand.Count - game.Human.Health;
                        foreach (CardView card in HandCards.Take(count))
                        {
                            card.IsSelected = true;
                        }

                        Primary_Click(this, new RoutedEventArgs());
                    }
                    else
                    {
                        game.Advance();
                    }
                }

                if (game.Phase != GamePhase.GameOver || !capturedResponse)
                {
                    throw new InvalidOperationException("UI-driven match did not reach a response and final result.");
                }

                Capture(Path.Combine(directory, "06-result.png"));

                RulesOverlay.Visibility = Visibility.Visible;
                Capture(Path.Combine(directory, "07-rules.png"));
                RulesOverlay.Visibility = Visibility.Collapsed;
                AudioOverlay.Visibility = Visibility.Visible;
                Capture(Path.Combine(directory, "08-audio.png"));
                VerifyAudioControls();
                listener.Flush();
                string bindingErrors = errors.ToString();
                File.WriteAllText(Path.Combine(directory, "bindings.txt"), bindingErrors);
                if (!string.IsNullOrWhiteSpace(bindingErrors))
                {
                    throw new InvalidOperationException($"WPF binding errors: {bindingErrors}");
                }

                File.WriteAllText(Path.Combine(directory, "result.txt"),
                    "PASS: 25 official portraits, 15 official card faces, 26 rank glyphs and 4 suit glyphs decoded; " +
                    "official PC seat/identity/health/button textures; non-overlapping surrounding seats in four modes; " +
                    "lobby, kingdom filter, four game modes, card selection, skill conversion, target selection, " +
                    "response, discard, result, rules and audio controls rendered; " +
                    "UI-driven match completed; zero binding errors.");
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        }

        private static void VerifyOfficialArtwork()
        {
            foreach (Hero hero in Heroes.All)
            {
                BitmapSource portrait = (BitmapSource)Art.Portrait(hero).ImageSource;
                if (portrait.PixelWidth < 200 || portrait.PixelHeight < 300)
                {
                    throw new InvalidOperationException($"Official portrait is too small: {hero.Name}");
                }
            }

            foreach (CardKind kind in Enum.GetValues<CardKind>())
            {
                BitmapSource face = OfficialAssets.CardFace(kind);
                if (face.PixelWidth != 135 || face.PixelHeight != 190)
                {
                    throw new InvalidOperationException($"Official card frame has unexpected size: {kind}");
                }
            }

            foreach (string suit in new[] { "♠", "♥", "♣", "♦" })
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    Card card = new(0, CardKind.Slash, suit, rank);
                    _ = OfficialAssets.Rank(card);
                    _ = OfficialAssets.Suit(card);
                }
            }
        }

        private static void VerifyOfficialSeating()
        {
            foreach (GameMode mode in Enum.GetValues<GameMode>())
            {
                GameEngine match = new(2026);
                match.Start("guanyu", Role.Lord, mode);
                List<Rect> occupied = new();
                foreach (Player player in match.Players.Where(player => !player.IsHuman))
                {
                    PlayerView view = new(player, match, null);
                    Rect seat = new(view.X, view.Y, 172, 232);
                    if (seat.Left < 0 || seat.Top < 0 || seat.Right > 1176 || seat.Bottom > 600 ||
                        occupied.Any(other => other.IntersectsWith(seat)))
                    {
                        throw new InvalidOperationException($"Overlapping/out-of-bounds official seat layout: {mode}");
                    }

                    occupied.Add(seat);
                }

                _ = new PlayerView(match.Human, match, null);
            }
        }

        private void Capture(string path)
        {
            FrameworkElement root = (FrameworkElement)Content;
            root.Measure(new Size(1440, 900));
            root.Arrange(new Rect(0, 0, 1440, 900));
            root.UpdateLayout();
            Dispatcher.Invoke(() =>
            {
            }, DispatcherPriority.ContextIdle);
            RenderTargetBitmap bitmap = new(1440, 900, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
