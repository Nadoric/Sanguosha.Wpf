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
            HasGame ? game.Human.Kingdom : selectedHero.Kingdom);
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

        public IReadOnlyList<EquipmentSlotView> EquipmentSlots => new[]
        {
            new EquipmentSlotView(OfficialInterface.TreasureSlot, "宝物栏", true),
            new EquipmentSlotView(OfficialInterface.WeaponSlot, WeaponLabel, WeaponLabel == "武器栏"),
            new EquipmentSlotView(OfficialInterface.ArmorSlot, ArmorLabel, ArmorLabel == "防具栏"),
            new EquipmentSlotView(OfficialInterface.OffensiveHorseSlot, "进攻坐骑", true),
            new EquipmentSlotView(OfficialInterface.HorseSlot, HorseLabel, HorseLabel == "坐骑栏")
        };
    }

    public sealed record EquipmentSlotView(BitmapSource Icon, string Label, bool IsEmpty)
    {
        public string Color => IsEmpty ? "#8A9A8F" : "#E6D1A0";
    }
}
