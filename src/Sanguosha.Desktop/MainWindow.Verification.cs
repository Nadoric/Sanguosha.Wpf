using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
                VerifyHeroGallery();
                VerifyOfficialSeating();
                VerifyTargetCardUi(directory);
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
                    if (game.PendingDecision is SkillDecisionRequest decision)
                    {
                        game.ChooseSkillDecision(decision.Options[0].Key);
                    }
                    else if (game.PendingCardChoice is TargetCardRequest choice)
                    {
                        TargetCardOption option = choice.Options[0];
                        game.ChooseTargetCard(option.Zone, option.CardId, option.HandPosition);
                    }
                    else if (game.Pending != null)
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
                        int count = game.Human.Hand.Count - game.Human.HandLimit;
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
                    $"PASS: {Heroes.All.Count} official portraits, 15 official card faces, 26 rank glyphs and 4 suit glyphs decoded; " +
                    "official PC seat/identity/health/button textures; non-overlapping surrounding seats in four modes; " +
                    "lobby, kingdom filter, four game modes, card selection, skill conversion, target selection, " +
                    "response, discard, result, rules, target-zone choice and steal result, audio controls rendered; " +
                    "UI-driven match completed; zero binding errors.");
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        }

        private void VerifyTargetCardUi(string directory)
        {
            GameEngine initial = new(42);
            initial.Start("zhangfei");
            initial.Advance();
            SavedGame snapshot = JsonSerializer.Deserialize<SavedGame>(initial.Save())!;
            Card MoveTo(CardKind kind, List<Card> destination)
            {
                IEnumerable<List<Card>> zones = new[] { snapshot.Deck, snapshot.Discard }
                    .Concat(snapshot.Players.SelectMany(player =>
                        new[] { player.Hand, player.Equipment, player.Judgments, player.SpecialCards ?? new() }.Concat(player.NamedZones?.Values ?? Enumerable.Empty<List<Card>>())));
                foreach (List<Card> zone in zones)
                {
                    Card? card = zone.FirstOrDefault(item => item.Kind == kind);
                    if (card != null)
                    {
                        zone.Remove(card);
                        destination.Add(card);
                        return card;
                    }
                }

                throw new InvalidOperationException($"Missing UI test card: {kind}");
            }

            // 固定目标为关羽，排除随机选到陆逊的谦逊或防御马干扰。
            int guanyuSeat = snapshot.Players.FindIndex(player => player.HeroId == "guanyu");
            if (guanyuSeat > 1)
            {
                SavedPlayer previous = snapshot.Players[1];
                snapshot.Players[1] = previous with { HeroId = "guanyu", Health = 4, MaxHealth = 4 };
                snapshot.Players[guanyuSeat] = snapshot.Players[guanyuSeat] with
                {
                    HeroId = previous.HeroId,
                    Health = previous.MaxHealth,
                    MaxHealth = previous.MaxHealth
                };
            }
            else if (guanyuSeat < 0)
            {
                snapshot.Players[1] = snapshot.Players[1] with
                {
                    HeroId = "guanyu",
                    Health = 4,
                    MaxHealth = 4
                };
            }

            Card trick = MoveTo(CardKind.Steal, snapshot.Players[0].Hand);
            Card weapon = MoveTo(CardKind.Blade, snapshot.Players[1].Equipment);
            MoveTo(CardKind.Indulgence, snapshot.Players[1].Judgments);
            BindGame(GameEngine.Load(JsonSerializer.Serialize(snapshot), 42));
            Refresh();
            Lobby.Visibility = Visibility.Collapsed;
            CardView chosen = HandCards.Single(card => card.Id == trick.Id);
            HandCard_Click(new Button { Tag = chosen }, new RoutedEventArgs());
            Target_Click(new Button { Tag = 1 }, new RoutedEventArgs());
            Primary_Click(this, new RoutedEventArgs());
            if (CardChoiceOverlay.Visibility != Visibility.Visible || CardChoiceButtons.Children.OfType<WrapPanel>().Count() != 3)
            {
                throw new InvalidOperationException("Steal did not display all three target card zones.");
            }

            Button[] choiceButtons = CardChoiceButtons.Children.OfType<WrapPanel>()
                .SelectMany(row => row.Children.OfType<Button>()).ToArray();
            Button[] hiddenButtons = choiceButtons.Where(button => button.Tag is TargetCardOption { Zone: CardZone.Hand }).ToArray();
            if (hiddenButtons.Length != game.Players[1].Hand.Count || hiddenButtons.Any(button =>
                button.Tag is not TargetCardOption { CardId: null, HandPosition: not null } ||
                ((StackPanel)button.Content).Children[0] is not Border))
                throw new InvalidOperationException("Hand choices did not show one identity-free card back per position.");
            Card lightning = new(-1, CardKind.Lightning, "♠", 1);
            Card shortage = new(-2, CardKind.SupplyShortage, "♠", 2);
            if (JudgmentFace(game.Players[1], lightning) != CardKind.Lightning ||
                JudgmentFace(game.Players[1], shortage) != CardKind.SupplyShortage)
                throw new InvalidOperationException("Judgment thumbnails were incorrectly forced to Indulgence.");
            Capture(Path.Combine(directory, "09-steal-choice.png"));
            Button button = CardChoiceButtons.Children.OfType<WrapPanel>().SelectMany(row => row.Children.OfType<Button>()).Single(item =>
                item.Tag is TargetCardOption { Zone: CardZone.Equipment });
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (CardChoiceOverlay.Visibility != Visibility.Collapsed ||
                !HandCards.Any(card => card.Id == weapon.Id) || !StatusLabel.Contains(weapon.Name))
            {
                throw new InvalidOperationException("Steal UI did not transfer equipment and display the result.");
            }

            Capture(Path.Combine(directory, "10-steal-result.png"));
            targets.Clear();
            Lobby.Visibility = Visibility.Visible;
        }

        private void VerifyHeroGallery() => VerifyGalleryReachability();

        private static void VerifyOfficialArtwork()
        {
            foreach (Hero hero in Heroes.All) _ = Art.Portrait(hero);
            PortraitCache.DrainForVerification();
            foreach (Hero hero in Heroes.All)
            {
                BitmapSource portrait = (BitmapSource)Art.Portrait(hero).ImageSource;
                if (portrait.PixelWidth < 200 || portrait.PixelWidth > 384 || portrait.PixelHeight < 300 || !portrait.IsFrozen)
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
            PortraitCache.DrainForVerification();
            RenderTargetBitmap bitmap = new(1440, 900, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
