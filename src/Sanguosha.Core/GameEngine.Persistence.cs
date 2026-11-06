using System.Text.Json;

namespace Sanguosha.Core
{
    public sealed record SavedPlayer(
        string HeroId,
        Role Role,
        bool IsAlive,
        int Health,
        int MaxHealth,
        int SlashCount,
        bool PlayedSlash,
        bool SkillUsed,
        List<Card> Hand,
        List<Card> Equipment,
        List<Card> Judgments);

    public sealed record SavedGame(
        int Version,
        GameMode Mode,
        int Round,
        List<SavedPlayer> Players,
        List<Card> Deck,
        List<Card> Discard,
        List<Card> Table,
        List<GameLog> Logs);

    public sealed partial class GameEngine
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            IgnoreReadOnlyProperties = true
        };

        public string Save()
        {
            if (!CanSave)
            {
                throw new InvalidOperationException("请在自己的出牌阶段保存，响应或结算期间不能保存。");
            }

            SavedGame snapshot = new(
                2,
                Mode,
                Round,
                Players.Select(player => new SavedPlayer(
                    player.Hero.Id,
                    player.Role,
                    player.IsAlive,
                    player.Health,
                    player.MaxHealth,
                    player.SlashCount,
                    player.PlayedSlash,
                    player.SkillUsed,
                    player.Hand.ToList(),
                    player.Equipment.ToList(),
                    player.Judgments.ToList())).ToList(),
                deck.ToList(),
                DiscardPile.ToList(),
                TableCards.ToList(),
                Logs.ToList());
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        public static GameEngine Load(string json, int? seed = null)
        {
            SavedGame snapshot = JsonSerializer.Deserialize<SavedGame>(json, JsonOptions)
                ?? throw new JsonException("存档内容为空。");
            ValidateSnapshot(snapshot);
            GameEngine game = new(seed);
            for (int seat = 0; seat < snapshot.Players.Count; seat++)
            {
                SavedPlayer saved = snapshot.Players[seat];
                Player player = new()
                {
                    Seat = seat,
                    Hero = Heroes.Find(saved.HeroId),
                    Role = saved.Role,
                    IsHuman = seat == 0,
                    Team = seat % 2,
                    IsAlive = saved.IsAlive,
                    Health = saved.Health,
                    MaxHealth = saved.MaxHealth,
                    SlashCount = saved.SlashCount,
                    PlayedSlash = saved.PlayedSlash,
                    SkillUsed = saved.SkillUsed
                };
                player.Hand.AddRange(saved.Hand);
                player.Equipment.AddRange(saved.Equipment);
                player.Judgments.AddRange(saved.Judgments);
                game.Players.Add(player);
            }

            game.deck.AddRange(snapshot.Deck);
            game.DiscardPile.AddRange(snapshot.Discard);
            game.TableCards.AddRange(snapshot.Table);
            game.Logs.AddRange(snapshot.Logs.TakeLast(200));
            game.logNumber = game.Logs.LastOrDefault()?.Number ?? 0;
            game.CurrentSeat = 0;
            game.Mode = snapshot.Mode;
            game.Round = snapshot.Round;
            game.Phase = GamePhase.Play;
            game.Status = "存档已恢复 · 继续你的出牌阶段。";
            game.AddLog("已读取存档，恢复对局。");
            return game;
        }

        private static void ValidateSnapshot(SavedGame snapshot)
        {
            int count = snapshot.Mode == GameMode.Identity8 ? 8 : snapshot.Mode == GameMode.Identity5 ? 5 : 4;
            if (snapshot.Version != 2 || !Enum.IsDefined(snapshot.Mode) || snapshot.Round < 1 || snapshot.Players == null ||
                snapshot.Players.Count != count || snapshot.Deck == null || snapshot.Discard == null ||
                snapshot.Table == null || snapshot.Logs == null || snapshot.Table.Count > 5 ||
                snapshot.Players.Any(player => player == null || player.Hand == null || player.Equipment == null || player.Judgments == null))
            {
                throw new JsonException("存档版本或结构不正确。");
            }

            List<Role> expected = snapshot.Mode switch
            {
                GameMode.Identity5 => new() { Role.Lord, Role.Loyalist, Role.Rebel, Role.Rebel, Role.Renegade },
                GameMode.Identity8 => new() { Role.Lord, Role.Loyalist, Role.Loyalist, Role.Rebel, Role.Rebel, Role.Rebel, Role.Rebel, Role.Renegade },
                GameMode.Team2V2 => new() { Role.Lord, Role.Rebel, Role.Loyalist, Role.Rebel },
                _ => Enum.GetValues<Role>().ToList()
            };
            if (!snapshot.Players.Select(player => player.Role).Order().SequenceEqual(expected.Order()) ||
                snapshot.Players.Any(player => !Enum.IsDefined(player.Role)) ||
                snapshot.Players.Select(player => player.HeroId).Distinct().Count() != count ||
                snapshot.Players.Any(player => !Heroes.All.Any(hero => hero.Id == player.HeroId)) ||
                !snapshot.Players[0].IsAlive)
            {
                throw new JsonException("存档的角色信息不正确。");
            }

            foreach (SavedPlayer player in snapshot.Players)
            {
                int maximum = Heroes.Find(player.HeroId).Health + (player.Role == Role.Lord && snapshot.Mode != GameMode.Team2V2 ? 1 : 0);
                if (player.MaxHealth != maximum || player.Health > maximum || player.SlashCount < 0 ||
                    (player.IsAlive && player.Health <= 0) ||
                    (!player.IsAlive && (player.Health > 0 || player.Hand.Count + player.Equipment.Count + player.Judgments.Count != 0)) ||
                    player.Equipment.Any(card => card == null || !card.IsEquipment) ||
                    player.Equipment.Select(EquipmentSlot).Distinct().Count() != player.Equipment.Count ||
                    player.Judgments.Count > 1 || player.Judgments.Any(card => card == null ||
                        (card.Kind != CardKind.Indulgence && card.Suit != "♦")))
                {
                    throw new JsonException("存档的体力或装备信息不正确。");
                }
            }

            GameEngine reference = new(0);
            reference.BuildDeck();
            Dictionary<int, Card> registry = reference.deck.ToDictionary(card => card.Id);
            List<Card> owned = snapshot.Deck.Concat(snapshot.Discard)
                .Concat(snapshot.Players.SelectMany(player => player.Hand.Concat(player.Equipment).Concat(player.Judgments))).ToList();
            if (owned.Count != 108 || owned.Any(card => card == null) ||
                owned.Select(card => card.Id).Distinct().Count() != 108 ||
                owned.Concat(snapshot.Table).Any(card => card == null ||
                    !registry.TryGetValue(card.Id, out Card? canonical) || card != canonical) ||
                snapshot.Logs.Any(log => log == null || log.Text == null))
            {
                throw new JsonException("存档的牌堆校验失败。");
            }

            List<SavedPlayer> alive = snapshot.Players.Where(player => player.IsAlive).ToList();
            bool ended = snapshot.Mode == GameMode.Team2V2
                ? snapshot.Players.Select((player, seat) => (player, seat))
                    .Where(item => item.player.IsAlive).Select(item => item.seat % 2).Distinct().Count() < 2
                : alive.All(player => player.Role != Role.Lord) || alive.All(player => player.Role is Role.Lord or Role.Loyalist);
            if (ended)
            {
                throw new JsonException("该对局已经结束。");
            }
        }
    }
}
