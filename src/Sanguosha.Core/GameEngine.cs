namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        private readonly Random random;
        private readonly Queue<Action> work = new();
        private readonly List<Card> deck = new();
        private int logNumber;

        public GameEngine(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public event Action? Changed;
        public List<Player> Players { get; } = new();
        public List<Card> DiscardPile { get; } = new();
        public List<Card> TableCards { get; } = new();
        public List<GameLog> Logs { get; } = new();
        public Player Human => Players.First(player => player.IsHuman);
        public Player Current => Players[CurrentSeat];
        public GameMode Mode { get; private set; }
        public string ModeName => Mode switch
        {
            GameMode.Identity5 => "五人身份",
            GameMode.Identity8 => "八人身份",
            GameMode.Team2V2 => "2V2 对战",
            _ => "四人身份"
        };
        public int CurrentSeat { get; private set; }
        public int Round { get; private set; }
        public int DeckCount => deck.Count;
        public GamePhase Phase { get; private set; }
        public ResponseRequest? Pending { get; private set; }
        public string Result { get; private set; } = "";
        public string Status { get; private set; } = "选择武将，开启山河之弈。";
        public bool HumanWon { get; private set; }
        public bool CanSave => Phase == GamePhase.Play && Current.IsHuman && Pending == null;

        public void Start(string heroId, Role humanRole = Role.Lord, GameMode mode = GameMode.Identity4)
        {
            Players.Clear();
            deck.Clear();
            DiscardPile.Clear();
            TableCards.Clear();
            Logs.Clear();
            work.Clear();
            Pending = null;
            Result = "";
            HumanWon = false;
            Round = 0;
            logNumber = 0;
            Mode = mode;

            List<Role> roles = mode switch
            {
                GameMode.Identity5 => new() { Role.Lord, Role.Loyalist, Role.Rebel, Role.Rebel, Role.Renegade },
                GameMode.Identity8 => new() { Role.Lord, Role.Loyalist, Role.Loyalist, Role.Rebel, Role.Rebel, Role.Rebel, Role.Rebel, Role.Renegade },
                GameMode.Team2V2 => new() { Role.Lord, Role.Rebel, Role.Loyalist, Role.Rebel },
                _ => new() { Role.Lord, Role.Loyalist, Role.Rebel, Role.Renegade }
            };
            if (mode != GameMode.Team2V2)
            {
                roles.Remove(humanRole);
                Shuffle(roles);
                roles.Insert(0, humanRole);
            }
            List<Hero> opponents = Heroes.All.Where(hero => hero.Id != heroId).ToList();
            Shuffle(opponents);

            for (int seat = 0; seat < roles.Count; seat++)
            {
                Role role = roles[seat];
                Hero hero = seat == 0 ? Heroes.Find(heroId) : opponents[seat - 1];
                int health = hero.Health + (role == Role.Lord && mode != GameMode.Team2V2 ? 1 : 0);
                Players.Add(new Player
                {
                    Seat = seat,
                    Hero = hero,
                    Role = role,
                    IsHuman = seat == 0,
                    Team = seat % 2,
                    MaxHealth = health,
                    Health = health
                });
            }

            BuildDeck();
            foreach (Player player in Players)
            {
                Draw(player, 4);
            }

            CurrentSeat = Players.FindIndex(player => player.Role == Role.Lord);
            Phase = GamePhase.Resolving;
            AddLog(mode == GameMode.Team2V2
                ? "2V2对战开始。你与三号位组成青龙队，一、三号位对抗二、四号位；消灭对方全队获胜。"
                : $"{ModeName}局开始。你的身份是{Human.RoleName}，主公为{Current.Hero.Name}。其余身份在阵亡时公开。");
            EmitCue(GameCueKind.MatchStarted);
            work.Enqueue(BeginTurn);
            Notify();
        }

        public void Advance()
        {
            if (Phase is GamePhase.GameOver or GamePhase.NotStarted || Pending != null)
            {
                return;
            }

            if (work.TryDequeue(out Action? action))
            {
                action();
                Notify();
            }
        }

        private void BeginTurn()
        {
            if (Round == 0 || CurrentSeat == Players.First(player => player.IsAlive).Seat)
            {
                Round++;
            }

            Current.SlashCount = 0;
            Current.PlayedSlash = false;
            Current.SkillUsed = false;
            EmitCue(GameCueKind.TurnStarted, Current);
            ResolveTurnStart(Current);
        }

        private void CompleteAction(Player actor)
        {
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            if (!actor.IsAlive)
            {
                NextTurn();
                return;
            }

            if (actor.IsHuman)
            {
                Phase = GamePhase.Play;
                Status = "出牌阶段 · 选择手牌，再选择目标；也可直接结束回合。";
            }
            else
            {
                Phase = GamePhase.Resolving;
                Status = $"{actor.Hero.Name}正在思考…";
                work.Enqueue(() => AiAction(actor));
            }
        }

        public bool EndTurn()
        {
            if (Phase != GamePhase.Play || !Current.IsHuman)
            {
                return false;
            }

            BeginDiscard(Current);

            Notify();
            return true;
        }

        public bool Discard(IEnumerable<int> cardIds)
        {
            if (Phase != GamePhase.Discard || !Current.IsHuman)
            {
                return false;
            }

            int excess = Current.Hand.Count - Math.Max(0, Current.Health);
            List<int> ids = cardIds.Distinct().ToList();
            if (ids.Count != excess || ids.Any(id => Current.Hand.All(card => card.Id != id)))
            {
                Status = $"请恰好选择 {excess} 张手牌。";
                Notify();
                return false;
            }

            foreach (int id in ids)
            {
                DiscardFromHand(Current, Current.Hand.First(card => card.Id == id));
            }

            AddLog($"{Current.Hero.Name}弃置{ids.Count}张牌。");
            EndActorTurn(Current);
            Notify();
            return true;
        }

        private void NextTurn()
        {
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            do
            {
                CurrentSeat = (CurrentSeat + 1) % Players.Count;
            }
            while (!Current.IsAlive);

            Phase = GamePhase.Resolving;
            Status = $"轮到{Current.Hero.Name}。";
            work.Enqueue(BeginTurn);
        }

        private void BuildDeck()
        {
            // 精简牌堆共108张：独立构建，不读取参考项目的脚本或资源。
            (CardKind Kind, int Count)[] distribution =
            {
                (CardKind.Slash, 30),
                (CardKind.Dodge, 21),
                (CardKind.Peach, 12),
                (CardKind.Duel, 5),
                (CardKind.DrawTwo, 6),
                (CardKind.Dismantle, 6),
                (CardKind.Steal, 5),
                (CardKind.Barbarians, 4),
                (CardKind.Arrows, 3),
                (CardKind.Feast, 2),
                (CardKind.Crossbow, 3),
                (CardKind.Indulgence, 3),
                (CardKind.Blade, 3),
                (CardKind.EightTrigrams, 2),
                (CardKind.Horse, 3)
            };

            string[] suits = { "♠", "♥", "♣", "♦" };
            int id = 1;
            foreach ((CardKind kind, int count) in distribution)
            {
                for (int index = 0; index < count; index++)
                {
                    deck.Add(new Card(id, kind, suits[(id - 1) % 4], (id - 1) % 13 + 1));
                    id++;
                }
            }

            Shuffle(deck);
        }

        private Card? TakeTop()
        {
            if (deck.Count == 0 && DiscardPile.Count > 0)
            {
                deck.AddRange(DiscardPile);
                DiscardPile.Clear();
                Shuffle(deck);
                AddLog("牌堆耗尽，洗混弃牌堆后继续。");
            }

            if (deck.Count == 0)
            {
                return null;
            }

            Card card = deck[^1];
            deck.RemoveAt(deck.Count - 1);
            return card;
        }

        private void Draw(Player player, int count)
        {
            for (int index = 0; index < count; index++)
            {
                Card? card = TakeTop();
                if (card != null)
                {
                    player.Hand.Add(card);
                }
            }
        }

        private void DiscardFromHand(Player player, Card card)
        {
            player.Hand.Remove(card);
            DiscardPile.Add(card);
            OnHandLost(player);
        }

        private void ShowCard(Card card)
        {
            TableCards.RemoveAll(item => item.Id == card.Id);
            TableCards.Add(card);
            if (TableCards.Count > 5)
            {
                TableCards.RemoveAt(0);
            }
        }

        private void Shuffle<T>(IList<T> values)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (values[index], values[other]) = (values[other], values[index]);
            }
        }

        private void AddLog(string message)
        {
            Logs.Add(new GameLog(++logNumber, message));
            if (Logs.Count > 200)
            {
                Logs.RemoveAt(0);
            }
        }

        private void Notify()
        {
            Changed?.Invoke();
        }
    }
}
