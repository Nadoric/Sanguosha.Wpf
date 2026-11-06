using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public abstract class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Raise([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public static class Art
    {
        private static readonly Dictionary<string, ImageBrush> Portraits = new();

        public static ImageBrush Portrait(Hero hero)
        {
            if (Portraits.TryGetValue(hero.Id, out ImageBrush? existing))
            {
                return existing;
            }

            ImageBrush brush = new(OfficialAssets.LoadBitmap($"Portraits/{hero.Id}.png"))
            {
                Stretch = Stretch.UniformToFill,
                AlignmentY = AlignmentY.Top
            };
            brush.Freeze();
            Portraits.Add(hero.Id, brush);
            return brush;
        }

        public static Brush Color(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
    }

    public sealed class HeroView
    {
        public HeroView(Hero hero)
        {
            Hero = hero;
        }

        public Hero Hero { get; }
        public string Name => Hero.Name;
        public string Kingdom => Hero.Kingdom;
        public string Skill => Hero.Skill;
        public string Description => Hero.Description;
        public ImageBrush Portrait => Art.Portrait(Hero);
    }

    public sealed class CardView : Observable
    {
        private bool selected;

        public CardView(Card card, bool available = true)
        {
            Card = card;
            Available = available;
        }

        public Card Card { get; }
        public int Id => Card.Id;
        public string Name => Card.Name;
        public string Suit => Card.Suit;
        public string Rank => Card.RankText;
        public string Category => Card.Category;
        public string Hint => $"{Card.Suit}{Card.RankText} · {Card.Name}\n{Card.Description}";
        public bool Available { get; }
        public BitmapSource Face => OfficialAssets.CardFace(Card.Kind);
        public BitmapSource RankImage => OfficialAssets.Rank(Card);
        public BitmapSource SuitImage => OfficialAssets.Suit(Card);

        public bool IsSelected
        {
            get => selected;
            set
            {
                selected = value;
                Raise();
            }
        }
    }

    public sealed class PlayerView
    {
        public PlayerView(Player player, GameEngine game, int? target)
        {
            Player = player;
            Name = player.Hero.Name;
            Portrait = Art.Portrait(player.Hero);
            Kingdom = player.Hero.Kingdom;
            Health = $"{Math.Max(0, player.Health)} / {player.MaxHealth}";
            Hearts = string.Concat(Enumerable.Repeat("● ", Math.Max(0, player.Health))) +
                string.Concat(Enumerable.Repeat("○ ", Math.Max(0, player.MaxHealth - Math.Max(0, player.Health))));
            Identity = game.Mode == GameMode.Team2V2
                ? player.Team == 0 ? "青龙队" : "朱雀队"
                : player.Role == Role.Lord || player.IsHuman || !player.IsAlive || game.Phase == GamePhase.GameOver
                ? player.RoleName
                : "身份未知";
            State = !player.IsAlive ? "已阵亡" : game.Phase == GamePhase.GameOver ? "存活" :
                player == game.Current ? "◈ 当前回合" : $"{player.Seat + 1}号位 · 等待";
            Hand = $"手牌  {player.Hand.Count:D2}";
            Distance = player.IsAlive && game.Human.IsAlive ? $"距离 {game.Distance(game.Human, player)}" : "距离 —";
            Skill = player.Hero.Skill;
            SkillDescription = player.Hero.Description;
            Equipment = player.Equipment.Count + player.Judgments.Count == 0
                ? "无装备"
                : string.Join(" · ", player.Equipment.Concat(player.Judgments).Select(card => card.Name));
            Border = Art.Color(target == player.Seat ? "#FFE3A0" :
                player == game.Current ? "#D0AA55" : "#715A3D");
            Opacity = player.IsAlive ? 1 : 0.42;
            IsSelected = target == player.Seat;
            IdentityImage = OfficialInterface.Identity(player, game);
            HealthPips = OfficialInterface.Health(player);
            Faction = OfficialInterface.Faction(player.Hero.Kingdom);
            SetPosition(game.Players.Count);
        }

        public Player Player { get; }
        public int Seat => Player.Seat;
        public string Name { get; }
        public string Kingdom { get; }
        public ImageBrush Portrait { get; }
        public string Health { get; }
        public string Hearts { get; }
        public string Identity { get; }
        public string State { get; }
        public string Hand { get; }
        public string Distance { get; }
        public string Skill { get; }
        public string SkillDescription { get; }
        public string Equipment { get; }
        public Brush Border { get; }
        public double Opacity { get; }
        public bool IsSelected { get; }
        public string VerticalName => string.Join("\n", Name.ToCharArray());
        public string HandCount => Player.Hand.Count.ToString();
        public string SeatLabel => $"{Seat + 1}号位";
        public string Detail => $"{Name} · {Identity}\n体力 {Health} · {Hand} · {Distance}\n" +
            $"{Skill}：{SkillDescription}\n装备：{Equipment}";
        public BitmapSource IdentityImage { get; }
        public BitmapSource Faction { get; }
        public IReadOnlyList<BitmapSource> HealthPips { get; }
        public double X { get; private set; }
        public double Y { get; private set; }

        private void SetPosition(int playerCount)
        {
            if (Seat == 0)
            {
                return;
            }

            // 座次自玩家左侧顺时针围桌排列；阵亡后不重新排列视觉座位。
            (double X, double Y)[] positions = playerCount switch
            {
                8 => new[]
                {
                    (22.0, 337.0), (22.0, 76.0), (270.0, 13.0), (500.0, 13.0),
                    (730.0, 13.0), (970.0, 76.0), (970.0, 337.0)
                },
                5 => new[] { (36.0, 220.0), (305.0, 16.0), (665.0, 16.0), (965.0, 220.0) },
                _ => new[] { (65.0, 205.0), (500.0, 16.0), (945.0, 205.0) }
            };
            (X, Y) = positions[Seat - 1];
        }
    }
}
