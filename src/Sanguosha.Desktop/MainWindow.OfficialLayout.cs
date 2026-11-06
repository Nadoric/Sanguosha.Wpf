using System.Windows.Media.Imaging;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        public BitmapSource BattleBackground => OfficialInterface.Background;
        public BitmapSource? HumanIdentityImage => HasGame
            ? OfficialInterface.Identity(game.Human, game)
            : null;
        public IReadOnlyList<BitmapSource> HumanHealthPips => HasGame
            ? OfficialInterface.Health(game.Human)
            : Array.Empty<BitmapSource>();
        public BitmapSource HumanFaction => OfficialInterface.Faction(
            HasGame ? game.Human.Hero.Kingdom : selectedHero.Kingdom);
        public string HumanVerticalName => string.Join("\n", HumanName.ToCharArray());
        public string WeaponLabel => HasGame
            ? game.Human.Equipment.FirstOrDefault(card =>
                card.Kind is CardKind.Blade or CardKind.Crossbow)?.Name ?? "武器栏"
            : "武器栏";
        public string ArmorLabel => HasGame
            ? game.Human.Equipment.FirstOrDefault(card =>
                card.Kind == CardKind.EightTrigrams)?.Name ?? "防具栏"
            : "防具栏";
        public string HorseLabel => HasGame
            ? game.Human.Equipment.FirstOrDefault(card => card.Kind == CardKind.Horse)?.Name ?? "坐骑栏"
            : "坐骑栏";
    }
}
