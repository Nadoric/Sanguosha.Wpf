using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public static class OfficialAssets
    {
        private static readonly BitmapImage CardAtlas = LoadBitmap("Cards/card.png");
        private static readonly Dictionary<string, Int32Rect> Frames = ReadFrames();
        private static readonly Dictionary<string, BitmapSource> Sprites = new();

        public static BitmapImage LoadBitmap(string relativePath)
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(
                $"pack://application:,,,/Assets/Official/{relativePath}",
                UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static Dictionary<string, Int32Rect> ReadFrames()
        {
            Uri uri = new("pack://application:,,,/Assets/Official/Cards/card.atlas");
            using Stream stream = Application.GetResourceStream(uri)!.Stream;
            using JsonDocument document = JsonDocument.Parse(stream);
            Dictionary<string, Int32Rect> result = new();
            foreach (JsonProperty property in document.RootElement.GetProperty("frames").EnumerateObject())
            {
                JsonElement frame = property.Value.GetProperty("frame");
                Int32Rect rectangle = new(
                    frame.GetProperty("x").GetInt32(),
                    frame.GetProperty("y").GetInt32(),
                    frame.GetProperty("w").GetInt32(),
                    frame.GetProperty("h").GetInt32());
                result.Add(property.Name, rectangle);
            }

            return result;
        }

        public static BitmapSource Sprite(string name)
        {
            if (Sprites.TryGetValue(name, out BitmapSource? existing))
            {
                return existing;
            }

            // 官网图集保持原始文件；仅按其配套atlas坐标在WPF中读取，不重绘牌面。
            CroppedBitmap sprite = new(CardAtlas, Frames[name]);
            sprite.Freeze();
            Sprites.Add(name, sprite);
            return sprite;
        }

        public static BitmapSource CardFace(Card card) => card.Kind == CardKind.Slash && card.Nature != DamageNature.Normal
            ? Sprite(card.Nature == DamageNature.Fire ? "HuoSha_png.png" : "LeiSha_png.png") : CardFace(card.Kind);

        public static BitmapSource CardFace(CardKind kind)
        {
            string name = kind switch
            {
                CardKind.Slash => "Sha",
                CardKind.Dodge => "Shan",
                CardKind.Peach => "Tao",
                CardKind.Duel => "JueDou",
                CardKind.DrawTwo => "WuZhongShengYou",
                CardKind.Dismantle => "GuoHeChaiQiao",
                CardKind.Steal => "ShunShouQianYang",
                CardKind.Barbarians => "NanManRuQin",
                CardKind.Arrows => "WanJianQiFa",
                CardKind.Feast => "TaoYuanJieYi",
                CardKind.Indulgence => "LeBuSiShu",
                CardKind.Crossbow => "ZhuGeLianNu",
                CardKind.Blade => "QingLongYanYueDao",
                CardKind.EightTrigrams => "BaGuaZhen",
                CardKind.Horse => "DiLu",
                CardKind.SupplyShortage => "BingLiangCunDuan",
                CardKind.IronChain => "TieSuoLianHuan",
                CardKind.Nullification => "WuXieKeJi",
                CardKind.FireAttack => "HuoGong",
                CardKind.Wine => "Jiu",
                CardKind.Lightning => "ShanDian",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            return Sprite($"{name}_png.png");
        }

        public static BitmapSource Rank(Card card)
        {
            return Sprite($"{(card.IsRed ? "red" : "black")}_{card.RankText}.png");
        }

        public static BitmapSource Suit(Card card)
        {
            string name = card.Suit switch
            {
                "♠" => "spade",
                "♥" => "heart",
                "♣" => "club",
                "♦" => "diamond",
                _ => throw new ArgumentException("未知花色。", nameof(card))
            };
            return Sprite($"{name}.png");
        }
    }
}
