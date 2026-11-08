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
        public static ImageBrush Portrait(Hero hero, bool grayscale = false)
        {
            return PortraitCache.Get(SkinLibrary.Shared.Current(hero.Id), grayscale);
        }

        public static ImageBrush SkinPortrait(HeroSkin skin)
        {
            return PortraitCache.Get(skin);
        }

        public static Brush Color(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
    }

    public sealed class HeroView : Observable
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

        public void RefreshPortrait()
        {
            Raise(nameof(Portrait));
        }
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
        public BitmapSource Face => OfficialAssets.CardFace(Card);
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
            Portrait = Art.Portrait(player.Hero, !player.IsAlive);
            Kingdom = player.Kingdom;
            Health = $"{player.Health} / {player.MaxHealth}";
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
            Skill = player.EffectiveSkillText + (player.Awakened ? " · 已觉醒" : "") + (player.LimitedUsed ? " · 限定技已用" : "") + (player.TurnedOver ? " · 背面" : "") + (player.Chained ? " · 连环" : "") + (player.SpecialCards.Count > 0 ? " · 不屈 " + string.Join("/", player.SpecialCards.Select(card => card.RankText)) : "");
            SkillDescription = player.Hero.Description + (player.NamedCards.Any() ? "\n" + string.Join("；", player.NamedZones.Select(pair => pair.Key + "：" + (pair.Key == "谦逊" && !player.IsHuman ? pair.Value.Count + "张暗置牌" : string.Join("、", pair.Value.Select(card => card.Suit + card.RankText))))) : "");
            if (player.Incarnations.Count > 0) SkillDescription += "\n化身池：" + (player.IsHuman ? string.Join("、", player.Incarnations.Select(id => Heroes.Find(id).Name)) : player.Incarnations.Count + "张（仅本人可见）");
            Equipment = player.Equipment.Count + player.Judgments.Count == 0
                ? "无装备"
                : string.Join(" · ", player.Equipment.Select(card => card.Name).Concat(player.Judgments.Select(card => player.SupplyShortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage ? "兵粮寸断" : card.Kind == CardKind.Lightning ? "闪电" : "乐不思蜀")));
            Border = Art.Color(target == player.Seat ? "#FFE3A0" :
                player == game.Current ? "#D0AA55" : "#715A3D");
            Opacity = player.IsAlive ? 1 : 0.8;
            IsSelected = target == player.Seat;
            IdentityImage = OfficialInterface.Identity(player, game);
            DeathStamp = OfficialInterface.DeathStamp(player, game.Mode);
            HealthPips = OfficialInterface.Health(player);
            Faction = OfficialInterface.Faction(player.Kingdom);
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
        public System.Windows.Visibility DeathVisibility => Player.IsAlive ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        public BitmapSource DeathStamp { get; }
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

            SceneSeat bounds = BattleSceneLayout.Opponent(playerCount, Seat);
            X = bounds.X; Y = bounds.Y;
        }
    }
}
