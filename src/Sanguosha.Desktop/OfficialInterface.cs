using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public static class OfficialInterface
    {
        private static readonly Dictionary<string, BitmapSource> Sprites = new();
        private static readonly Dictionary<string, BitmapImage> Atlases = new();
        private static readonly Dictionary<string, Dictionary<string, Int32Rect>> Frames = new();

        public static BitmapSource Background { get; } = OfficialAssets.LoadBitmap(
            "Interface/battle-background-house.png");

        public static BitmapSource Confirm => Get("selfseat", "SpellOptSelectorButton_normal.png");
        public static BitmapSource ConfirmHover => Get("selfseat", "SpellOptSelectorButton_over.png");
        public static BitmapSource Cancel => Get("selfseat", "SpellOptCancelButton_normal.png");
        public static BitmapSource Skill => Get("selfseat", "skill_common_up.png");
        public static BitmapSource OwnFrame => Get("seat_bottom", "genearlBaseBg1.png");
        public static BitmapSource OwnEquipment => Get("seat_bottom", "selfSeatEquipBg.png");
        public static BitmapSource WeaponSlot => Get("selfseat", "self_equip_wuqi.png");
        public static BitmapSource ArmorSlot => Get("selfseat", "self_equip_fangju.png");
        public static BitmapSource HorseSlot => Get("selfseat", "self_equip_jiayima.png");
        public static BitmapSource HandBadge => Get("seat", "handCardsBg.png");
        public static BitmapSource NameBar => Get("seat", "TableSeatNameBg.png");

        public static BitmapSource Get(string atlas, string name)
        {
            string key = $"{atlas}/{name}";
            if (Sprites.TryGetValue(key, out BitmapSource? existing))
            {
                return existing;
            }

            if (!Atlases.ContainsKey(atlas))
            {
                Atlases.Add(atlas, OfficialAssets.LoadBitmap($"Interface/{atlas}.png"));
                Uri uri = new($"pack://application:,,,/Assets/Official/Interface/{atlas}.atlas");
                using Stream stream = Application.GetResourceStream(uri)!.Stream;
                using JsonDocument document = JsonDocument.Parse(stream);
                Dictionary<string, Int32Rect> entries = new();
                foreach (JsonProperty entry in document.RootElement.GetProperty("frames").EnumerateObject())
                {
                    JsonElement frame = entry.Value.GetProperty("frame");
                    entries.Add(entry.Name, new Int32Rect(
                        frame.GetProperty("x").GetInt32(),
                        frame.GetProperty("y").GetInt32(),
                        frame.GetProperty("w").GetInt32(),
                        frame.GetProperty("h").GetInt32()));
                }

                Frames.Add(atlas, entries);
            }

            CroppedBitmap sprite = new(Atlases[atlas], Frames[atlas][name]);
            sprite.Freeze();
            Sprites.Add(key, sprite);
            return sprite;
        }

        public static BitmapSource Identity(Player player, GameEngine game)
        {
            string key;
            if (game.Mode == GameMode.Team2V2)
            {
                key = player.Team == game.Human.Team ? "Figure_Teamer" : "Figure_Enemy";
            }
            else if (player.IsHuman || player.Role == Role.Lord || !player.IsAlive ||
                game.Phase == GamePhase.GameOver)
            {
                key = player.Role switch
                {
                    Role.Lord => "Figure_ZhuGong_Face",
                    Role.Loyalist => "Figure_ZhongChen_Face",
                    Role.Rebel => "Figure_FanZei_Face",
                    _ => "Figure_NeiJian_Face"
                };
            }
            else
            {
                key = "Figure_Mask_None";
            }

            return Get("seat", $"{key}.png");
        }

        public static IReadOnlyList<BitmapSource> Health(Player player)
        {
            List<BitmapSource> result = new();
            string color = player.Health <= 1 ? "redBlood" : player.Health <= 2 ? "yellowBlood" : "greenBlood";
            for (int index = 0; index < player.MaxHealth; index++)
            {
                result.Add(Get("seat", $"{(index < player.Health ? color : "empBlood")}.png"));
            }

            return result;
        }

        public static BitmapSource Faction(string kingdom)
        {
            string key = kingdom switch
            {
                "魏" => "wei",
                "蜀" => "shu",
                "吴" => "wu",
                _ => "qun"
            };
            return Get("seat", $"{key}Bg.png");
        }
    }
}
