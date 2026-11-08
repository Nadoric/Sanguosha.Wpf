using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        internal void RunSkinVerification(string directory)
        {
            Directory.CreateDirectory(directory);
            timer.Stop();
            using StringWriter errors = new();
            using TextWriterTraceListener listener = new(errors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            try
            {
                SkinLibrary library = SkinLibrary.Shared;
                if (library.All.Count != Heroes.All.Count * 3 || Heroes.All.Any(hero => library.ForHero(hero.Id).Count != 3))
                {
                    throw new InvalidOperationException("Expected three official skins for every registered hero.");
                }

                foreach (HeroSkin skin in library.All) _ = skin.Portrait;
                PortraitCache.DrainForVerification();
                foreach (HeroSkin skin in library.All)
                {
                    BitmapSource bitmap = (BitmapSource)skin.Portrait.ImageSource;
                    if (bitmap.PixelWidth < 200 || bitmap.PixelWidth > 384 || bitmap.PixelHeight < 300 || !bitmap.IsFrozen)
                    {
                        throw new InvalidOperationException($"Invalid skin image: {skin.HeroId}/{skin.Id}");
                    }
                }

                string preferencesPath = Path.Combine(directory, "skin-preferences.json");
                File.WriteAllText(preferencesPath, "{}");
                SkinLibrary preferences = new(preferencesPath);
                HeroSkin alternative = preferences.ForHero("guanyu")[1];
                if (!preferences.Apply("guanyu", alternative.Id, out _) ||
                    new SkinLibrary(preferencesPath).Current("guanyu").Id != alternative.Id ||
                    preferences.Apply("liubei", alternative.Id, out _))
                {
                    throw new InvalidOperationException("Skin save/reload or hero ownership validation failed.");
                }

                File.WriteAllText(preferencesPath, "{broken");
                if (new SkinLibrary(preferencesPath).Current("guanyu").Id != "classic")
                {
                    throw new InvalidOperationException("Corrupt preferences did not fall back to classic.");
                }

                BindGame(new GameEngine(42));
                game.Start("guanyu");
                game.Advance();
                SavedGame snapshot = JsonSerializer.Deserialize<SavedGame>(game.Save())!;
                foreach (CardKind kind in new[] { CardKind.Blade, CardKind.EightTrigrams, CardKind.Horse })
                {
                    Card card = snapshot.Deck.First(item => item.Kind == kind);
                    snapshot.Deck.Remove(card);
                    snapshot.Players[0].Hand.Add(card);
                }

                BindGame(GameEngine.Load(JsonSerializer.Serialize(snapshot), 42));
                Lobby.Visibility = Visibility.Collapsed;
                Refresh();
                foreach (Card card in game.Human.Hand.Where(item => item.IsEquipment).ToArray())
                {
                    game.Play(card.Id);
                }

                ClearBattleEffects();
                Capture(Path.Combine(directory, "01-equipment-layout.png"));
                VerifyEquipmentColumns();
                string saved = game.Save();
                ImageBrush before = HumanPortrait;
                Skins_Click(this, new RoutedEventArgs());
                SkinChoicesList.SelectedIndex = 1;
                if (library.Current("guanyu").Id != "classic" || !ReferenceEquals(before, HumanPortrait))
                {
                    throw new InvalidOperationException("Preview changed a skin before confirmation.");
                }

                Capture(Path.Combine(directory, "02-skin-preview.png"));
                ApplySkin_Click(this, new RoutedEventArgs());
                if (ReferenceEquals(before, HumanPortrait) ||
                    !ReferenceEquals(HumanPortrait, alternative.Portrait) || saved != game.Save())
                {
                    throw new InvalidOperationException("Skin application did not refresh the portrait or changed rules.");
                }

                CloseSkins_Click(this, new RoutedEventArgs());
                Capture(Path.Combine(directory, "03-skin-applied.png"));
                Skins_Click(this, new RoutedEventArgs());
                SkinHeroList.SelectedItem = SkinHeroes.Single(hero => hero.Hero.Id == game.Players[1].Hero.Id);
                SkinChoicesList.SelectedIndex = 1;
                ApplySkin_Click(this, new RoutedEventArgs());
                if (!ReferenceEquals(Opponents.Single(player => player.Seat == 1).Portrait,
                    library.Current(game.Players[1].Hero.Id).Portrait))
                {
                    throw new InvalidOperationException("Opponent skin did not update.");
                }

                SkinHeroList.SelectedItem = SkinHeroes.Single(hero => hero.Hero.Id == "guanyu");
                SkinChoicesList.SelectedIndex = 0;
                ApplySkin_Click(this, new RoutedEventArgs());
                if (!ReferenceEquals(before, HumanPortrait))
                {
                    throw new InvalidOperationException("Restoring classic skin failed.");
                }

                CloseSkins_Click(this, new RoutedEventArgs());
                Lobby.Visibility = Visibility.Visible;
                HeroView selected = (HeroView)HeroList.SelectedItem;
                bool refreshed = false;
                selected.PropertyChanged += (_, args) =>
                {
                    refreshed |= args.PropertyName == nameof(HeroView.Portrait);
                };
                Skins_Click(this, new RoutedEventArgs());
                SkinChoicesList.SelectedIndex = 1;
                ApplySkin_Click(this, new RoutedEventArgs());
                if (!refreshed || SkinLibrary.Shared.Current(selected.Hero.Id).Id == "classic")
                {
                    throw new InvalidOperationException("Lobby skin entry or portrait refresh failed.");
                }

                CloseSkins_Click(this, new RoutedEventArgs());
                Capture(Path.Combine(directory, "04-lobby-skin.png"));
                listener.Flush();
                File.WriteAllText(Path.Combine(directory, "bindings.txt"), errors.ToString());
                if (!string.IsNullOrWhiteSpace(errors.ToString()))
                {
                    throw new InvalidOperationException($"Skin UI binding errors: {errors}");
                }

                File.WriteAllText(Path.Combine(directory, "result.txt"),
                    $"PASS: {library.All.Count} official portraits decoded for {Heroes.All.Count} heroes; preview, apply, opponent refresh, " +
                    "classic restoration, preference persistence and corrupt-preference fallback verified; " +
                    "no rule-state changes; five equipment rows have separate icon/text bounds; " +
                    "zero binding errors. Native renders, not manual interaction tests.");
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        }

        private void VerifyEquipmentColumns()
        {
            for (int index = 0; index < EquipmentItems.Items.Count; index++)
            {
                ContentPresenter presenter = (ContentPresenter)EquipmentItems.ItemContainerGenerator
                    .ContainerFromIndex(index);
                Image icon = (Image)presenter.ContentTemplate.FindName("SlotIcon", presenter);
                TextBlock label = (TextBlock)presenter.ContentTemplate.FindName("SlotLabel", presenter);
                Point iconPoint = icon.TranslatePoint(new Point(), EquipmentItems);
                Point labelPoint = label.TranslatePoint(new Point(), EquipmentItems);
                if (labelPoint.X < iconPoint.X + icon.ActualWidth + 4 ||
                    labelPoint.X + label.ActualWidth > EquipmentItems.ActualWidth + 1 ||
                    labelPoint.Y < index * 31 || labelPoint.Y + label.ActualHeight > (index + 1) * 31)
                {
                    throw new InvalidOperationException($"Overlapping/out-of-bounds equipment label: row {index}");
                }
            }
        }
    }
}
