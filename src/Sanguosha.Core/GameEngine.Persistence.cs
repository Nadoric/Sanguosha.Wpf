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
        List<Card> Judgments,
        bool? DuelJudgmentRed = null,
        int ZhaXiangCount = 0,
        bool HujiaDrawUsed = false,
        bool TurnedOver = false,
        bool Chained = false,
        bool Awakened = false,
        bool LimitedUsed = false,
        bool SkillsDisabled = false,
        bool LuoyiActive = false,
        int TianyiState = 0,
        int RendeGiven = 0,
        List<Card>? SpecialCards = null,
        List<int>? LuoshenCards = null,
        List<int>? SupplyShortages = null,
        bool JijiangFailed = false,
        Dictionary<string, int>? Marks = null,
        Dictionary<string, List<Card>>? NamedZones = null,
        List<string>? Incarnations = null, string? IncarnationHeroId = null, string? IncarnationSkill = null,
        List<SkillCategory>? TemporarySkillBans = null, bool HandUseBanned = false);

    public sealed record SavedGame(
        int Version,
        GameMode Mode,
        int Round,
        List<SavedPlayer> Players,
        List<Card> Deck,
        List<Card> Discard,
        List<Card> Table,
        List<GameLog> Logs,
        List<int>? ExtraTurns = null, int? ExtraTurnOrigin = null, List<string>? DiscardedSuits = null,
        Dictionary<int, int>? MatchKills = null, Dictionary<int, int>? TurnKills = null);

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
                5,
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
                    player.Judgments.ToList(),
                    player.DuelJudgmentRed,
                    player.ZhaXiangCount,
                    player.HujiaDrawUsed,
                    player.TurnedOver, player.Chained, player.Awakened, player.LimitedUsed,
                    player.SkillsDisabled, player.LuoyiActive, player.TianyiState, player.RendeGiven,
                    player.SpecialCards.ToList(), player.LuoshenCards.ToList(), player.SupplyShortages.ToList(), player.JijiangFailed, new(player.Marks), player.NamedZones.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()), player.Incarnations.ToList(), player.IncarnationHeroId, player.IncarnationSkill, player.TemporarySkillBans.ToList(), player.HandUseBanned)).ToList(),
                deck.ToList(),
                DiscardPile.ToList(),
                TableCards.ToList(),
                Logs.ToList(), extraTurns.ToList(), extraTurnOrigin, discardedSuits.ToList(), new(matchKills), new(turnKills));
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        public static GameEngine Load(string json, int? seed = null)
        {
            SavedGame snapshot = JsonSerializer.Deserialize<SavedGame>(json, JsonOptions)
                ?? throw new JsonException("存档内容为空。");
            ValidateSnapshot(snapshot);
            GameEngine game = new(seed);
            if (snapshot.Version == 2)
            {
                GameEngine reference = new(0); reference.BuildDeck();
                Dictionary<int, Card> registry = reference.deck.ToDictionary(card => card.Id);
                foreach (List<Card> zone in new[] { snapshot.Deck, snapshot.Discard, snapshot.Table }.Concat(snapshot.Players.SelectMany(player => new[] { player.Hand, player.Equipment, player.Judgments, player.SpecialCards ?? new() })))
                    for (int index = 0; index < zone.Count; index++) zone[index] = registry[zone[index].Id];
            }
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
                    SkillUsed = saved.SkillUsed,
                    DuelJudgmentRed = saved.DuelJudgmentRed,
                    ZhaXiangCount = saved.ZhaXiangCount,
                    HujiaDrawUsed = saved.HujiaDrawUsed,
                    TurnedOver = saved.TurnedOver, Chained = saved.Chained, Awakened = saved.Awakened,
                    LimitedUsed = saved.LimitedUsed, SkillsDisabled = saved.SkillsDisabled, LuoyiActive = saved.LuoyiActive || snapshot.Version == 2 && seat == 0 && saved.HeroId == "xuchu",
                    TianyiState = saved.TianyiState, RendeGiven = snapshot.Version == 2 && saved.HeroId == "liubei" && saved.SkillUsed ? Math.Max(2, saved.RendeGiven) : saved.RendeGiven, InPlayPhase = seat == 0, JijiangFailed = saved.JijiangFailed, IncarnationHeroId = saved.IncarnationHeroId, IncarnationSkill = saved.IncarnationSkill, HandUseBanned = saved.HandUseBanned
                };
                player.Hand.AddRange(saved.Hand);
                player.Equipment.AddRange(saved.Equipment);
                player.Judgments.AddRange(saved.Judgments);
                player.SpecialCards.AddRange(saved.SpecialCards ?? new());
                player.LuoshenCards.UnionWith(saved.LuoshenCards ?? new());
                player.SupplyShortages.UnionWith(saved.SupplyShortages ?? new());
                foreach (var pair in saved.Marks ?? new()) player.Marks.Add(pair.Key, pair.Value);
                foreach (var pair in saved.NamedZones ?? new()) player.NamedZones.Add(pair.Key, pair.Value.ToList());
                player.Incarnations.AddRange(saved.Incarnations ?? new()); player.TemporarySkillBans.UnionWith(saved.TemporarySkillBans ?? new());
                game.Players.Add(player);
            }

            foreach (int seat in snapshot.ExtraTurns ?? new()) game.extraTurns.Enqueue(seat);
            game.extraTurnOrigin = snapshot.ExtraTurnOrigin;
            foreach (var pair in snapshot.MatchKills ?? new()) game.matchKills.Add(pair.Key, pair.Value);
            foreach (var pair in snapshot.TurnKills ?? new()) game.turnKills.Add(pair.Key, pair.Value);
            game.discardedSuits.UnionWith(snapshot.DiscardedSuits ?? new());
            game.PhysicalCardCount = snapshot.Deck.Count + snapshot.Discard.Count + snapshot.Players.Sum(player => player.Hand.Count + player.Equipment.Count + player.Judgments.Count + (player.SpecialCards?.Count ?? 0) + (player.NamedZones?.Values.Sum(cards => cards.Count) ?? 0));
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
            game.ConfigureDynamicHandLimits();
            return game;
        }

        private static void ValidateSnapshot(SavedGame snapshot)
        {
            int count = snapshot.Mode == GameMode.Identity8 ? 8 : snapshot.Mode == GameMode.Identity5 ? 5 : 4;
            if (snapshot.Version is not (2 or 3 or 4 or 5) || !Enum.IsDefined(snapshot.Mode) || snapshot.Round < 1 || snapshot.Players == null ||
                snapshot.Players.Count != count || snapshot.Deck == null || snapshot.Discard == null ||
                snapshot.Table == null || snapshot.Logs == null || snapshot.Table.Count > 5 ||
                snapshot.Players.Any(player => player == null || player.Hand == null || player.Equipment == null || player.Judgments == null || player.Hand.Any(card => card == null) || player.Equipment.Any(card => card == null) ||
                    player.Judgments.Any(card => card == null) || player.SpecialCards?.Any(card => card == null) == true || player.NamedZones?.Any(pair => pair.Value == null || pair.Value.Any(card => card == null)) == true))
            {
                throw new JsonException("存档版本或结构不正确。");
            }

            int deaths = snapshot.Players.Count(player => !player.IsAlive);
            if (snapshot.MatchKills?.Any(pair => pair.Key < 0 || pair.Key >= count || pair.Value < 0 || pair.Value > deaths) == true ||
                (snapshot.MatchKills?.Values.Sum(value => (long)value) ?? 0) > deaths ||
                snapshot.TurnKills?.Any(pair => pair.Key < 0 || pair.Key >= count || pair.Value < 0 ||
                    pair.Value > (snapshot.MatchKills?.GetValueOrDefault(pair.Key) ?? 0)) == true)
                throw new JsonException("击杀特效计数不正确。");

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

            if (snapshot.Players.Select(player => Heroes.Find(player.HeroId))
                .Select(hero => hero.BaseHeroId ?? hero.Id).Distinct().Count() != count)
            {
                throw new JsonException("同一武将的标准版与界限突破版不能同时出现在一局中。");
            }

            if (snapshot.ExtraTurns?.Any(seat => seat < 0 || seat >= count) == true || snapshot.ExtraTurnOrigin is { } origin && (origin < 0 || origin >= count) ||
                snapshot.DiscardedSuits?.Any(suit => suit is not ("♠" or "♥" or "♣" or "♦")) == true) throw new JsonException("额外回合或弃牌记录不正确。");
            foreach (SavedPlayer player in snapshot.Players)
            {
                int maximum = Heroes.Find(player.HeroId).Health + (player.Role == Role.Lord && snapshot.Mode != GameMode.Team2V2 ? 1 : 0);
                if (player.Awakened && player.HeroId is "jiangwei" or "dengai" or "sunce" or "re_lvmeng") maximum--;
                if (player.Awakened && player.HeroId == "liushan") maximum++;
                List<Card> special = player.SpecialCards ?? new();
                List<int> shortages = player.SupplyShortages ?? new();
                bool buqu = (player.HeroId == "zhoutai" || player.HeroId == "zuoci" && player.IncarnationSkill == "不屈") && !player.SkillsDisabled && player.TemporarySkillBans?.Contains(SkillCategory.Ordinary) != true && special.Count == Math.Max(0, 1 - player.Health) && special.Select(card => card.Rank).Distinct().Count() == special.Count;
                if (player.TianyiState is < -1 or > 1 || (player.TianyiState != 0 && !player.SkillUsed) ||
                    (player.LuoshenCards?.Distinct().Count() != player.LuoshenCards?.Count) ||
                    (player.LuoshenCards?.Any(id => !player.Hand.Any(card => card.Id == id)) == true) || (player.TianyiState != 0 && player.HeroId is not ("taishici" or "zuoci")) || player.RendeGiven < 0 ||
                    (player.Marks?.Any(pair => pair.Key.Length > 64 || pair.Value < 0 || pair.Value > 1000000) == true) ||
                    (player.NamedZones?.Any(pair => pair.Key is not ("田" or "谦逊") || pair.Value.Count > 0 && (pair.Key == "田" ? player.HeroId is not ("dengai" or "zuoci") : player.HeroId is not ("re_luxun" or "zuoci"))) == true) ||
                    (special.Count > 0 && player.HeroId is not ("zhoutai" or "zuoci")) ||
                    (player.Awakened && player.HeroId is not ("jiangwei" or "dengai" or "sunce" or "liushan" or "re_lvmeng")) || (player.LimitedUsed && player.HeroId is not ("pangtong" or "jiaxu" or "re_ganning" or "re_zhangfei")) ||
                    (player.LuoyiActive && player.HeroId is not ("xuchu" or "re_xuchu" or "zuoci")) ||
                    (player.LuoshenCards?.Count > 0 && player.HeroId is not ("re_zhenji" or "zuoci")) ||
                    shortages.Distinct().Count() != shortages.Count || shortages.Any(id => !player.Judgments.Any(card => card.Id == id && !card.IsRed && (card.IsBasic || card.IsEquipment))) ||
                    player.ZhaXiangCount < 0 || (player.ZhaXiangCount != 0 && player.HeroId is not ("re_huanggai" or "zuoci")) ||
                    (player.DuelJudgmentRed.HasValue && player.HeroId is not ("yanliangwenchou" or "zuoci")) ||
                    (player.HeroId is "dongzhuo" or "zuoci" ? player.MaxHealth < (player.IsAlive ? 1 : 0) || player.MaxHealth > maximum : player.MaxHealth != maximum) || player.Health > player.MaxHealth || player.SlashCount < 0 ||
                    (player.IsAlive && player.Health <= 0 && !buqu) ||
                    (!player.IsAlive && (player.Health > 0 || player.Hand.Count + player.Equipment.Count + player.Judgments.Count + special.Count + (player.NamedZones?.Values.Sum(cards => cards.Count) ?? 0) != 0)) ||
                    player.Equipment.Any(card => card == null || !card.IsEquipment) ||
                    player.Equipment.Select(EquipmentSlot).Distinct().Count() != player.Equipment.Count ||
                    player.Judgments.Count > 3 || player.Judgments.Count(card => shortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage) > 1 || player.Judgments.Count(card => !shortages.Contains(card.Id) && card.Kind is not (CardKind.Lightning or CardKind.SupplyShortage)) > 1 || player.Judgments.Count(card => card.Kind == CardKind.Lightning) > 1 || player.Judgments.Any(card => card == null ||
                        (!shortages.Contains(card.Id) && card.Kind is not (CardKind.Indulgence or CardKind.Lightning or CardKind.SupplyShortage) && card.Suit != "♦")))
                {
                    throw new JsonException("存档的体力或装备信息不正确。");
                }
            }

            HashSet<string> liveNames = snapshot.Players.Where(player => player.IsAlive).Select(player => GeneralIdentity(Heroes.Find(player.HeroId))).ToHashSet(StringComparer.Ordinal);
            HashSet<string> souls = new(StringComparer.Ordinal);
            foreach (SavedPlayer player in snapshot.Players)
            {
                List<string> forms = player.Incarnations ?? new();
                if (player.TemporarySkillBans?.Any(category => !Enum.IsDefined(category)) == true || player.TemporarySkillBans?.Distinct().Count() != player.TemporarySkillBans?.Count ||
                    forms.Count > Heroes.All.Count || forms.Any(id => id == null || !Heroes.All.Any(hero => hero.Id == id && SkillCatalog.Borrowable(hero).Count > 0)) ||
                    (player.HeroId != "zuoci" || !player.IsAlive || player.SkillsDisabled) && (forms.Count > 0 || player.IncarnationHeroId != null || player.IncarnationSkill != null) ||
                    (player.IncarnationHeroId == null) != (player.IncarnationSkill == null) ||
                    player.IncarnationHeroId != null && (!forms.Contains(player.IncarnationHeroId) || !SkillCatalog.Borrowable(Heroes.Find(player.IncarnationHeroId)).Any(skill => skill.Name == player.IncarnationSkill)))
                    throw new JsonException("化身、所选单项技能或临时技能类别禁用记录不正确。");
                foreach (string id in forms)
                {
                    string name = GeneralIdentity(Heroes.Find(id));
                    if (liveNames.Contains(name) || !souls.Add(name)) throw new JsonException("化身池与在场武将或其他化身重复。");
                }
            }

            GameEngine reference = new(0);
            reference.BuildDeck();
            Dictionary<int, Card> registry = reference.deck.ToDictionary(card => card.Id);
            List<Card> owned = snapshot.Deck.Concat(snapshot.Discard)
                .Concat(snapshot.Players.SelectMany(player => player.Hand.Concat(player.Equipment).Concat(player.Judgments).Concat(player.SpecialCards ?? new()).Concat(player.NamedZones?.Values.SelectMany(cards => cards) ?? Enumerable.Empty<Card>()))).ToList();
            if (owned.Count is not (108 or 119) || owned.Any(card => card == null) ||
                owned.Select(card => card.Id).Distinct().Count() != owned.Count || owned.Any(card => card.Id < 1 || card.Id > owned.Count) ||
                owned.Concat(snapshot.Table).Any(card => card == null ||
                    !registry.TryGetValue(card.Id, out Card? canonical) || (card != canonical && !(snapshot.Version == 2 && card == canonical with { Nature = DamageNature.Normal }))) ||
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
