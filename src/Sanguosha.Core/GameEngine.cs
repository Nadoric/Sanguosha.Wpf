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
            DiscardPile = new ObservedDiscardPile(card => { if (Phase != GamePhase.NotStarted && card.Suit is "♠" or "♥" or "♣" or "♦") discardedSuits.Add(card.Suit); });
        }

        public event Action? Changed;
        public List<Player> Players { get; } = new();
        public ObservedDiscardPile DiscardPile { get; }
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
        public TargetCardRequest? PendingCardChoice { get; private set; }
        public SkillDecisionRequest? PendingDecision { get; private set; }
        public string Result { get; private set; } = "";
        public string Status { get; private set; } = "选择武将，开启山河之弈。";
        public bool HumanWon { get; private set; }
        public bool CanSave => cardUses.Count == 0 && reservedUseMaterials.Count == 0 && judgmentMaterials.Count == 0 && !resolvingReaction && reactions.Count == 0 && OrderingCards.Count == 0 && Phase == GamePhase.Play && Current.IsHuman && Pending == null && PendingDecision == null && PendingCardChoice == null;

        public void Start(string heroId, Role humanRole = Role.Lord, GameMode mode = GameMode.Identity4)
        {
            normalDrawSeat = null; finishingGlobalTurn = false; skippedIncarnationTurn = false; HandEvents.Clear(); handLossBatches.Clear();
            Phase = GamePhase.NotStarted; cardUses.Clear(); reservedUseMaterials.Clear(); judgmentMaterials.Clear(); CardEvents.Clear(); extraTurns.Clear(); extraTurnOrigin = null; useNumber = 0; dyingPlayers.Clear(); pendingTuntian.Clear(); discardedSuits.Clear();
            matchKills.Clear(); turnKills.Clear();
            Players.Clear();
            deck.Clear();
            DiscardPile.Clear();
            TableCards.Clear();
            Logs.Clear();
            work.Clear();
            Pending = null;
            PendingCardChoice = null;
            PendingDecision = null;
            discardingActor = null; phaseDiscards.Clear(); usedSlashNatures.Clear(); aiRecasted.Clear(); reactions.Clear(); OrderingCards.Clear(); equipmentLossCounts.Clear(); resolvingReaction = false;
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
            Hero selected = Heroes.Find(heroId);
            string selectedIdentity = selected.BaseHeroId ?? selected.Id;
            // A historical general and its refreshed variant are mutually exclusive in a match.
            List<Hero> opponents = Heroes.All.Where(hero => (hero.BaseHeroId ?? hero.Id) != selectedIdentity)
                .GroupBy(hero => hero.BaseHeroId ?? hero.Id)
                .Select(group => group.ElementAt(random.Next(group.Count()))).ToList();
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

            ConfigureDynamicHandLimits();
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
            work.Enqueue(() => InitializeIncarnations(0, BeginTurn));
            Notify();
        }

        public void Advance()
        {
            if (Phase is GamePhase.GameOver or GamePhase.NotStarted ||
                Pending != null || PendingCardChoice != null || PendingDecision != null)
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
            turnKills.Clear(); // Every real/extra/skipped turn starts a new counting window.
            if (Round == 0 || !extraTurnOrigin.HasValue && CurrentSeat == Players.First(player => player.IsAlive).Seat)
            {
                Round++;
            }

            discardedSuits.Clear();
            ResetBatchThreeTurn(Current);
            Current.LuoyiActive = false;
            if (Current.TurnedOver) { skippedIncarnationTurn = true; TurnOver(Current); NextTurn(); return; }
            aiRecasted.Remove(Current.Seat);
            Current.JijiangFailed = false; Current.LuoshenCards.Clear(); Current.RendeGiven = 0; Current.TianyiState = 0; Current.InPlayPhase = false;
            Current.SlashCount = 0;
            Current.PlayedSlash = false;
            Current.SkillUsed = false;
            Current.DuelJudgmentRed = null;
            Current.ZhaXiangCount = 0;
            foreach (Player player in Players) player.HujiaDrawUsed = false;
            EmitCue(GameCueKind.TurnStarted, Current);
            ChooseIncarnation(Current, true, () => ResolveTurnStart(Current));
        }

        private void CompleteAction(Player actor)
        {
            if (DrainReactions(() => CompleteAction(actor))) return;
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            if (!actor.IsAlive)
            {
                NextTurn();
                return;
            }

            actor.InPlayPhase = true;
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

            int excess = Current.Hand.Count - Current.HandLimit;
            List<int> ids = cardIds.Distinct().ToList();
            if (ids.Count != excess || ids.Any(id => Current.Hand.All(card => card.Id != id)))
            {
                Status = $"请恰好选择 {excess} 张手牌。";
                Notify();
                return false;
            }

            PayCards(Current, ids.Select(id => Current.Hand.First(card => card.Id == id)).ToList());

            AddLog($"{Current.Hero.Name}弃置{ids.Count}张牌。");
            EndActorTurn(Current);
            Notify();
            return true;
        }

        private void NextTurn()
        {
            if (DrainReactions(NextTurn)) return;
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            FinishGlobalTurn(NextTurnCore);
        }

        private void NextTurnCore()
        {
            if (Phase == GamePhase.GameOver) return;
            if (extraTurnOrigin.HasValue) { CurrentSeat = extraTurnOrigin.Value; extraTurnOrigin = null; }
            while (extraTurns.Count > 0 && !Players[extraTurns.Peek()].IsAlive) extraTurns.Dequeue();
            if (extraTurns.Count > 0) { extraTurnOrigin = CurrentSeat; CurrentSeat = extraTurns.Dequeue(); }
            else { do { CurrentSeat = (CurrentSeat + 1) % Players.Count; } while (!Current.IsAlive); }

            Phase = GamePhase.Resolving;
            Status = $"轮到{Current.Hero.Name}。";
            work.Enqueue(BeginTurn);
        }

        private void BuildDeck()
        {
            // Retain IDs 1–108 for old saves; append 11 explicit-suit expansion cards. Custom distribution, not a full official deck.
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
                    deck.Add(new Card(id, kind, suits[(id - 1) % 4], (id - 1) % 13 + 1,
                        kind == CardKind.Slash && id is >= 22 and <= 29 ? (id % 2 == 0 ? DamageNature.Fire : DamageNature.Thunder) : DamageNature.Normal));
                    id++;
                }
            }

            foreach (var item in new[] {
                (CardKind.Nullification, "♠", 11), (CardKind.Nullification, "♣", 12), (CardKind.Nullification, "♠", 13), (CardKind.Nullification, "♣", 13),
                (CardKind.Wine, "♠", 3), (CardKind.Wine, "♣", 9), (CardKind.FireAttack, "♥", 2), (CardKind.FireAttack, "♦", 12),
                (CardKind.IronChain, "♠", 11), (CardKind.SupplyShortage, "♣", 4), (CardKind.Lightning, "♠", 1) })
            { deck.Add(new Card(id++, item.Item1, item.Item2, item.Item3)); }
            PhysicalCardCount = deck.Count;
            Shuffle(deck);
        }

        private Card? TakeTop()
        {
            if (deck.Count == 0 && DiscardPile.Count > 0)
            {
                List<Card> recyclable = DiscardPile.Where(card => !InFlight(card)).ToList();
                deck.AddRange(recyclable);
                foreach (Card recycled in recyclable) DiscardPile.Remove(recycled);
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
            List<Card> cards = new();
            for (int index = 0; index < count; index++)
            {
                Card? card = TakeTop();
                if (card != null)
                {
                    cards.Add(card);
                }
            }
            Gain(player, cards);
        }

        private void DiscardFromHand(Player player, Card card)
        {
            if (!player.Hand.Remove(card)) return;
            PublishHand(player, false, new[] { card });
            if (handLossBatches.TryGetValue(player.Seat, out var lost)) lost.Add(card.Id);
            DiscardPile.Add(card);
            if (player == discardingActor) phaseDiscards.Add(card);
            if (!handLossBatches.ContainsKey(player.Seat)) OnHandLost(player);
        }

        private void ShowCard(Card card)
        {
            if (card.Id <= 0) return;
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
