using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        private Hero skinHero = Heroes.All[0];
        private string skinStatus = "选择皮肤预览，点击应用后生效；仅更换形象，沿用当前技能与语音。";

        public ObservableCollection<HeroView> SkinHeroes { get; } = new();
        public ObservableCollection<HeroSkin> SkinChoices { get; } = new();
        public string SkinTitle => $"武将衣橱 · {skinHero.Name}";
        public string SkinStatus => skinStatus;
        public string SkinCurrentLabel => $"当前：{SkinLibrary.Shared.Current(skinHero.Id).Name}";
        public bool CanApplySkin => SkinChoicesList?.SelectedItem is HeroSkin;

        private void InitializeSkinSelector()
        {
            foreach (Hero hero in Heroes.All)
            {
                SkinHeroes.Add(new HeroView(hero));
            }
        }

        private void Skins_Click(object sender, RoutedEventArgs e)
        {
            audio.StopAll();
            ClearBattleEffects();
            skinHero = Lobby.Visibility == Visibility.Visible || !HasGame ? selectedHero : game.Human.Hero;
            SkinHeroList.SelectedItem = SkinHeroes.Single(item => item.Hero.Id == skinHero.Id);
            PopulateSkinChoices();
            SkinsOverlay.Visibility = Visibility.Visible;
            RaiseAll();
        }

        private void PopulateSkinChoices()
        {
            SkinChoices.Clear();
            foreach (HeroSkin skin in SkinLibrary.Shared.ForHero(skinHero.Id))
            {
                SkinChoices.Add(skin);
            }

            SkinChoicesList.SelectedItem = SkinLibrary.Shared.Current(skinHero.Id);
            skinStatus = "选择皮肤预览，点击应用后生效；仅更换形象，沿用当前技能与语音。";
            RaiseAll();
        }

        private void SkinHero_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (SkinHeroList.SelectedItem is HeroView hero)
            {
                skinHero = hero.Hero;
                PopulateSkinChoices();
            }
        }

        private void SkinChoice_Changed(object sender, SelectionChangedEventArgs e)
        {
            RaiseAll();
        }

        private void ApplySkin_Click(object sender, RoutedEventArgs e)
        {
            if (SkinChoicesList.SelectedItem is not HeroSkin skin ||
                !SkinLibrary.Shared.Apply(skinHero.Id, skin.Id, out string message))
            {
                return;
            }

            skinStatus = message;
            ClearBattleEffects();
            foreach (HeroView hero in HeroChoices.Concat(SkinHeroes))
            {
                hero.RefreshPortrait();
            }

            if (HasGame)
            {
                Refresh();
            }

            RaiseAll();
        }

        private void CloseSkins_Click(object sender, RoutedEventArgs e)
        {
            SkinsOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
