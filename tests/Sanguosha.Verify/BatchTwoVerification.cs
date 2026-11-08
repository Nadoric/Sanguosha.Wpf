using System.Reflection;
using System.Text.Json;
using Sanguosha.Core;

namespace Sanguosha.Verify;

internal static partial class Program
{
    private static void Call(GameEngine game, string method, params object?[] args)
    {
        MethodInfo target = typeof(GameEngine).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] supplied = target.GetParameters().Select((parameter, index) => index < args.Length ? args[index] : parameter.DefaultValue).ToArray();
        target.Invoke(game, supplied);
    }

    private static void Decision(GameEngine game, string key)
        => Check(game.ChooseSkillDecision(key), $"Explicit decision accepted: {key}; {game.Status}");

    private static void FirstDecision(GameEngine game)
    {
        Check(game.PendingDecision != null, "Expected a concrete skill choice");
        Decision(game, game.PendingDecision!.Options[0].Key);
    }

    private static Card PutTop(SavedGame snapshot, Func<Card, bool> predicate)
    {
        Card card = snapshot.Deck.First(predicate); snapshot.Deck.Remove(card); snapshot.Deck.Add(card); return card;
    }

    private static void CheckAllCards(GameEngine game)
    {
        Check(game.DeckCount + game.DiscardPile.Count + game.OrderingCards.Count + game.Players.Sum(player => player.Hand.Count + player.Equipment.Count + player.Judgments.Count + player.SpecialCards.Count + player.NamedCards.Count()) == game.PhysicalCardCount,
            "All physical cards remain in owned zones, including private/public special piles");
    }

    private static void VerifyBatchTwo()
    {
        VerifyInteractiveJudgment();
        VerifyFlipAndShensu();
        VerifyTransferAndBuqu();
        VerifyChainAndNiepan();
        VerifyTianyiAndDuanliang();
        VerifyYinghunAndLusu();
        VerifyAwakeningAndGuzheng();
        VerifyRefreshedBatchTwo();
        VerifyBeigeAndDuanchang();
        VerifyBatchTwoPersistence();
        VerifyStandardCorrections();
        VerifyOptionalLossReactions();
        VerifyRebelAlliance();
        VerifyLianyingBeforeRetaliation();
    }

    private static void VerifyInteractiveJudgment()
    {
        SavedGame snapshot = ExpansionRig("simayi", "guojia");
        Card replacement = Move(snapshot, CardKind.Dodge, 0);
        PutTop(snapshot, card => card.Suit == "♠");
        GameEngine game = Restore(snapshot); Card? final = null;
        Call(game, "Judge", game.Players[1], "八卦阵", (Action<Card?>)(card => final = card));
        Check(game.PendingDecision!.Options.Any(option => option.Key == replacement.Id.ToString()), "Standard Guicai exposes own hand only");
        Decision(game, replacement.Id.ToString());
        Check(final == replacement && game.Players[1].Hand.Contains(replacement) && !game.DiscardPile.Contains(replacement), "Guicai replaces before Guojia Tiandu obtains final judgment");
        CheckAllCards(game);

        snapshot = ExpansionRig("re_simayi", "guojia");
        Card equipment = Move(snapshot, CardKind.Blade, 0); snapshot.Players[0].Hand.Remove(equipment); snapshot.Players[0].Equipment.Add(equipment);
        game = Restore(snapshot); Call(game, "Judge", game.Players[1], "八卦阵", (Action<Card?>)(_ => { }));
        Check(game.PendingDecision!.Options.Any(option => option.Key == equipment.Id.ToString()), "Refreshed Guicai also accepts equipment");
        Decision(game, equipment.Id.ToString()); Check(game.Players[1].Hand.Contains(equipment), "Equipment replacement becomes Tiandu hand card"); CheckAllCards(game);

        snapshot = ExpansionRig("zhugeliang", "caocao"); game = Restore(snapshot);
        List<Card> oldTop = snapshot.Deck.TakeLast(4).Reverse().ToList(); int completed = 0;
        Call(game, "Guanxing", game.Human, (Action)(() => completed++));
        Decision(game, $"bottom:{oldTop[0].Id}");
        foreach (Card card in oldTop.Skip(1)) Decision(game, $"top:{card.Id}");
        Check(completed == 1 && game.DeckCount == snapshot.Deck.Count, "Guanxing honors living-player count and neither gains nor discards cards"); CheckAllCards(game);
    }

    private static void VerifyFlipAndShensu()
    {
        SavedGame snapshot = ExpansionRig("caoren", "caocao"); GameEngine game = Restore(snapshot); int hand = game.Human.Hand.Count;
        Call(game, "EndActorTurn", game.Human); Decision(game, "yes");
        Check(game.Human.TurnedOver && game.Human.Hand.Count == hand + 3, "Classic Jushou draws three and flips at end");
        snapshot = ExpansionRig("caoren", "caocao"); snapshot.Players[0] = snapshot.Players[0] with { TurnedOver = true };
        game = Restore(snapshot); int deck = game.DeckCount; Call(game, "BeginTurn");
        Check(!game.Human.TurnedOver && game.Current != game.Human && game.DeckCount == deck, "A face-down turn flips back and skips preparation/draw/play");

        snapshot = ExpansionRig("xiahouyuan", "caocao");
        Card delayed = Move(snapshot, CardKind.Indulgence, 0); snapshot.Players[0].Hand.Remove(delayed); snapshot.Players[0].Judgments.Add(delayed);
        game = Restore(snapshot); deck = game.DeckCount; int health = game.Players[1].Health;
        Call(game, "ResolveTurnStart", game.Human); Decision(game, "yes"); Decision(game, "1");
        Check(game.Human.Judgments.Contains(delayed) && game.DeckCount == deck && game.Players[1].Health == health - 1, "Shensu one retains skipped judgment and replaces draw with virtual Slash");
        Check(game.Phase == GamePhase.Play && game.Human.SlashCount == 0, "Pre-play Shensu does not consume play-phase Slash quota");

        snapshot = ExpansionRig("xiahouyuan", "caocao"); Card weapon = Move(snapshot, CardKind.Blade, 0); game = Restore(snapshot);
        Call(game, "TryShensu", game.Human, false, (Action)(() => throw new Exception("Shensu unexpectedly declined")));
        Decision(game, "yes"); Decision(game, "1"); Decision(game, weapon.Id.ToString());
        Check(game.DiscardPile.Contains(weapon) && game.Players[1].Health == snapshot.Players[1].Health - 1, "Shensu two pays an equipment card and skips play"); CheckAllCards(game);
        snapshot = ExpansionRig("xiahouyuan", "zhugeliang");
        snapshot.Players[1] = snapshot.Players[1] with { Role = Role.Rebel };
        snapshot.Players[2] = snapshot.Players[2] with { Role = Role.Loyalist, IsAlive = false, Health = 0 };
        snapshot.Players[3] = snapshot.Players[3] with { Role = Role.Renegade, IsAlive = false, Health = 0 };
        game = Restore(snapshot); bool skipped = false;
        Call(game, "TryShensu", game.Human, true, (Action)(() => skipped = true));
        Check(skipped && game.PendingDecision == null, "Shensu with only an empty-hand Kongcheng survivor has no legal target and cannot open a mandatory null-target choice"); CheckAllCards(game);
    }

    private static void VerifyTransferAndBuqu()
    {
        SavedGame snapshot = ExpansionRig("xiaoqiao", "caocao");
        Card spade = snapshot.Deck.First(card => card.Suit == "♠"); snapshot.Deck.Remove(spade); snapshot.Players[0].Hand.Add(spade);
        GameEngine game = Restore(snapshot); int ownHealth = game.Human.Health, targetHealth = game.Players[2].Health; bool done = false;
        Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => done = true), null);
        Decision(game, "yes"); Decision(game, spade.Id.ToString()); Decision(game, "2");
        Check(done && game.Human.Health == ownHealth && game.Players[2].Health == targetHealth - 1, "Hongyan spade is a legal Tianxiang heart cost and transfers damage");
        Check(game.Players[2].Hand.Count == 1 && game.DiscardPile.Contains(spade), "Tianxiang survivor draws lost-health count"); CheckAllCards(game);

        snapshot = ExpansionRig("zhoutai", "caocao"); snapshot.Players[0] = snapshot.Players[0] with { Health = 1 };
        Card top = PutTop(snapshot, card => card.Rank == 5); game = Restore(snapshot); done = false;
        Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => done = true), null); Decision(game, "yes");
        Check(done && game.Human.IsAlive && game.Human.Health == 0 && game.Human.SpecialCards.SequenceEqual(new[] { top }), "Unique classic Buqu card prevents entering dying at zero HP"); CheckAllCards(game);

        snapshot = ExpansionRig("zhoutai", "caocao");
        Card first = snapshot.Deck.First(card => card.Rank == 5); snapshot.Deck.Remove(first);
        snapshot.Players[0] = snapshot.Players[0] with { Health = 0, SpecialCards = new() { first } };
        top = PutTop(snapshot, card => card.Rank == 5); Card peach = Move(snapshot, CardKind.Peach, 0); game = Restore(snapshot); done = false;
        Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => done = true), null); Decision(game, "yes");
        Check(game.Pending?.Kind == ResponseKind.Peach, "Duplicate Buqu ranks enter real rescue");
        Check(game.Respond(peach.Id), "Buqu duplicate can be rescued with a Peach"); FirstDecision(game);
        Check(done && game.Human.IsAlive && game.Human.Health == 0 && game.Human.SpecialCards.Count == 1, "Removing a duplicate after recovery restores Buqu at zero HP"); CheckAllCards(game);
    }

    private static void VerifyChainAndNiepan()
    {
        SavedGame snapshot = ExpansionRig("pangtong", "caocao");
        Card club = snapshot.Deck.First(card => card.Suit == "♣"); snapshot.Deck.Remove(club); snapshot.Players[0].Hand.Add(club); GameEngine game = Restore(snapshot);
        Check(game.UseSkill(club.Id, null), "Lianhuan accepts a club hand card"); Decision(game, "chain"); Decision(game, "1"); Decision(game, "2");
        Check(game.Players[1].Chained && game.Players[2].Chained && game.DiscardPile.Contains(club), "Lianhuan toggles exactly two distinct targets"); CheckAllCards(game);
        snapshot = ExpansionRig("pangtong", "caocao"); club = snapshot.Deck.First(card => card.Suit == "♣"); snapshot.Deck.Remove(club); snapshot.Players[0].Hand.Add(club); game = Restore(snapshot);
        Check(game.UseSkill(club.Id, null), "Lianhuan supports recast separately"); Decision(game, "recast"); Check(game.Human.Hand.Count == 1, "Recast discards one and draws one");

        snapshot = ExpansionRig("zhangfei", "caocao");
        Card fire = snapshot.Deck.First(card => card.Nature == DamageNature.Fire); snapshot.Deck.Remove(fire); snapshot.Players[0].Hand.Add(fire);
        snapshot.Players[1] = snapshot.Players[1] with { Chained = true }; snapshot.Players[2] = snapshot.Players[2] with { Chained = true };
        game = Restore(snapshot); Check(game.Play(fire.Id, 1), "Physical Fire Slash plays using the normal Slash rules");
        for (int index = 0; index < 10 && game.Phase == GamePhase.Resolving; index++) game.Advance();
        Check(game.Players[1].Health == snapshot.Players[1].Health - 1 && game.Players[2].Health == snapshot.Players[2].Health - 1, "Attribute damage propagates once to every linked character");
        Check(!game.Players[1].Chained && !game.Players[2].Chained, "Attribute damage resets affected chain state"); CheckAllCards(game);

        snapshot = ExpansionRig("pangtong", "caocao"); snapshot.Players[0] = snapshot.Players[0] with { Health = 1, Chained = true, TurnedOver = true };
        Move(snapshot, CardKind.Dodge, 0); game = Restore(snapshot); bool done = false;
        Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => done = true), null); Decision(game, "yes");
        Check(done && game.Human.Health == 3 && game.Human.Hand.Count == 3 && game.Human.LimitedUsed && !game.Human.Chained && !game.Human.TurnedOver, "Niepan discards, restores three, draws three and resets once"); CheckAllCards(game);
    }

    private static void VerifyTianyiAndDuanliang()
    {
        SavedGame snapshot = ExpansionRig("taishici", "caocao");
        Card high = snapshot.Deck.First(card => card.Rank == 13); snapshot.Deck.Remove(high); snapshot.Players[0].Hand.Add(high);
        Card low = snapshot.Deck.First(card => card.Rank == 1); snapshot.Deck.Remove(low); snapshot.Players[1].Hand.Add(low);
        Card slash = Move(snapshot, CardKind.Slash, 0); gameTianyi(snapshot, high, slash);
        void gameTianyi(SavedGame state, Card highCard, Card attack)
        {
            GameEngine game = Restore(state); Check(game.UseSkill(null, 1), "Tianyi starts real simultaneous hidden-card Pindian"); Decision(game, highCard.Id.ToString());
            Check(game.Human.TianyiState == 1 && game.ValidatePlay(game.Human, attack, game.Players[2]) == null, "Won Tianyi grants unlimited distance");
            Check(game.Play(attack.Id, 1), "Tianyi slash accepted"); Decision(game, "2");
            Check(game.Players[1].Health == state.Players[1].Health - 1 && game.Players[2].Health == state.Players[2].Health - 1, "Tianyi Slash resolves two targets"); CheckAllCards(game);
        }
        snapshot = ExpansionRig("xuhuang", "caocao"); Card black = snapshot.Deck.First(card => !card.IsRed && card.Kind <= CardKind.Peach); snapshot.Deck.Remove(black); snapshot.Players[0].Hand.Add(black); GameEngine tested = Restore(snapshot);
        Check(tested.Play(black.Id, 1, true), "Duanliang converts black basic/equipment into Supply Shortage");
        Check(tested.Players[1].SupplyShortages.Contains(black.Id) && tested.Players[1].Judgments.Contains(black), "Converted delayed trick keeps immutable physical identity plus separate kind");
        Check(GameEngine.Load(tested.Save()).Players[1].SupplyShortages.Contains(black.Id), "Supply Shortage identity survives save/load");
        int count = tested.Players[1].Hand.Count;
        Call(tested, "ResolveJudgments", tested.Players[1], false, false, (Action<bool, bool>)((_, skip) => Check(skip == (snapshot.Deck.Last().Suit != "♣"), "Supply Shortage uses club judgment")));
        Check(tested.Players[1].Hand.Count == count && !tested.Players[1].SupplyShortages.Any(), "Delayed identity is cleaned after judgment"); CheckAllCards(tested);
    }

    private static void VerifyYinghunAndLusu()
    {
        SavedGame snapshot = ExpansionRig("sunjian", "caocao"); snapshot.Players[0] = snapshot.Players[0] with { Health = 2 }; GameEngine game = Restore(snapshot);
        Call(game, "Preparation", game.Human, (Action)(() => { })); Decision(game, "1"); Decision(game, "draw");
        Check(game.Players[1].Hand.Count == 2, "Yinghun with X=3 draws three and discards one"); CheckAllCards(game);
        snapshot = ExpansionRig("lusu", "caocao"); for (int index = 0; index < 4; index++) Move(snapshot, CardKind.Slash, 0);
        game = Restore(snapshot); Call(game, "ResolveDrawPhase", game.Human, false); Decision(game, "yes"); FirstDecision(game);
        for (int index = 0; index < 4; index++) FirstDecision(game);
        Check(game.Human.Hand.Count == 4 && game.Players.Skip(1).Sum(player => player.Hand.Count) == 4, "Haoshi gives floor(half) to a minimum-hand other character"); CheckAllCards(game);
        snapshot = ExpansionRig("lusu", "caocao"); Move(snapshot, CardKind.Dodge, 0); Move(snapshot, CardKind.Slash, 1); Move(snapshot, CardKind.Peach, 1); Move(snapshot, CardKind.Duel, 2);
        game = Restore(snapshot); Check(game.UseSkill(null, 1, 2), "Dimeng accepts hand difference cost"); FirstDecision(game);
        Check(game.Players[1].Hand.Count == 1 && game.Players[2].Hand.Count == 2 && game.Human.Hand.Count == 0 && game.Human.SkillUsed, "Dimeng pays one and exchanges both whole hands once"); CheckAllCards(game);
    }

    private static void VerifyAwakeningAndGuzheng()
    {
        SavedGame snapshot = ExpansionRig("jiangwei", "caocao"); GameEngine game = Restore(snapshot); int max = game.Human.MaxHealth;
        Call(game, "Preparation", game.Human, (Action)(() => Call(game, "CompleteAction", game.Human))); Decision(game, "draw");
        for (int index = 0; index < 4; index++) FirstDecision(game);
        Check(game.Human.Awakened && game.Human.MaxHealth == max - 1 && game.Human.Hand.Count == 2, "Zhiji wakes once, reduces maximum HP and gains immediate Guanxing");
        Check(game.UseSkill(null, 1), "Tiaoxin can challenge a player whose range includes Jiangwei"); Check(game.Human.SkillUsed, "Tiaoxin counts as used even if target has neither Slash nor cards");
        snapshot = ExpansionRig("zhangzhaozhanghong", "caocao"); Card equipment = Move(snapshot, CardKind.Blade, 0); game = Restore(snapshot);
        Check(game.UseSkill(equipment.Id, 1) && game.Players[1].Equipment.Contains(equipment) && game.Human.Hand.Count == 1, "Zhijian puts equipment directly into an empty slot and draws one");
        snapshot = ExpansionRig("zhangzhaozhanghong", "caocao"); for (int index = 0; index < 6; index++) Move(snapshot, CardKind.Dodge, 1);
        game = Restore(snapshot); Call(game, "BeginDiscard", game.Players[1]); Decision(game, "yes"); FirstDecision(game);
        Check(game.Human.Hand.Count == 1 && game.Players[1].Hand.Count == 5, "Guzheng returns one discarded hand card and obtains the rest"); CheckAllCards(game);
    }

    private static void VerifyRefreshedBatchTwo()
    {
        SavedGame snapshot = ExpansionRig("re_xuchu", "caocao"); GameEngine game = Restore(snapshot); Call(game, "ResolveDrawPhase", game.Human, false); Decision(game, "yes");
        Check(game.Human.LuoyiActive && game.Human.Hand.All(card => card.IsBasic || card.Kind is CardKind.Duel or CardKind.Blade or CardKind.Crossbow), "Refreshed Luoyi obtains exactly permitted revealed card types");
        snapshot = ExpansionRig("re_zhenji", "caocao"); PutTop(snapshot, card => !card.IsRed); game = Restore(snapshot); bool done = false;
        Call(game, "Luoshen", game.Human, 108, (Action)(() => done = true)); Decision(game, "yes"); game.Advance(); Decision(game, "no");
        Check(done && game.Human.LuoshenCards.Count == 1 && game.Human.HandLimit == game.Human.Health + 1, "Refreshed Luoshen cards are excluded only from this turn's hand limit");
        snapshot = ExpansionRig("re_sunshangxiang", "caocao"); snapshot.Players[0] = snapshot.Players[0] with { Health = 2 }; Card blade = Move(snapshot, CardKind.Blade, 0); snapshot.Players[0].Hand.Remove(blade); snapshot.Players[0].Equipment.Add(blade);
        game = Restore(snapshot); Check(game.UseSkill(null, 1), "Refreshed Jieyin accepts an equipped weapon cost"); Decision(game, $"equip:{blade.Id}"); Decision(game, "yes");
        Check(game.Human.Health == 3 && game.Human.Hand.Count == 2 && game.Players[1].Equipment.Contains(blade) && game.Players[1].Hand.Count == 1, "Refreshed Jieyin transfers equipment, triggers Xiaoji, heals lower HP and draws for higher HP");
        snapshot = ExpansionRig("re_simayi", "xuchu"); snapshot.Players[1] = snapshot.Players[1] with { LuoyiActive = true }; Move(snapshot, CardKind.Dodge, 1); Move(snapshot, CardKind.Peach, 1);
        game = Restore(snapshot); Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => { }), CardKind.Slash);
        for (int index = 0; index < 2; index++) { Decision(game, "yes"); Check(game.ChooseTargetCard(CardZone.Hand), "Refankui obtains one hidden hand card for each damage point"); }
        Check(game.Human.Hand.Count == 2 && game.Players[1].Hand.Count == 0, "Refankui resolves twice for two damage");
        snapshot = ExpansionRig("liubei", "re_lvbu", "zhangfei", "sunquan"); Card gift = Move(snapshot, CardKind.Dodge, 0); game = Restore(snapshot);
        Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => { }), CardKind.Slash); Decision(game, "2");
        Check(game.Players[1].Hand.Contains(gift) && game.Players[2].Health == snapshot.Players[2].Health - 1, "Liyu victim chooses third party before Lü Bu gains a card and uses virtual Duel"); CheckAllCards(game);
    }

    private static void VerifyBeigeAndDuanchang()
    {
        SavedGame snapshot = ExpansionRig("caiwenji", "caocao", "zhangfei", "sunquan"); Card cost = Move(snapshot, CardKind.Dodge, 0); PutTop(snapshot, card => card.Suit == "♠"); GameEngine game = Restore(snapshot);
        Call(game, "Damage", game.Players[2], game.Players[1], null, (Action)(() => { }), CardKind.Slash); Decision(game, "yes"); Decision(game, cost.Id.ToString());
        Check(game.Players[2].TurnedOver && game.DiscardPile.Contains(cost), "Beige spade flips the actual damage source after a paid choice"); CheckAllCards(game);
        snapshot = ExpansionRig("caiwenji", "zhangfei"); snapshot.Players[0] = snapshot.Players[0] with { Health = 1 }; game = Restore(snapshot);
        Call(game, "Damage", game.Players[1], game.Human, null, (Action)(() => { }), null); Check(game.Respond(null), "Cai Wenji can decline real dying rescue");
        Check(!game.Human.IsAlive && game.Players[1].SkillsDisabled && game.Players[1].RuleId == "", "Duanchang permanently removes killer's active and passive general skills");
    }

    private static void VerifyBatchTwoPersistence()
    {
        SavedGame snapshot = ExpansionRig("zhoutai", "caocao"); Card special = snapshot.Deck[0]; snapshot.Deck.RemoveAt(0);
        snapshot.Players[0] = snapshot.Players[0] with { Health = 0, SpecialCards = new() { special }, Chained = true, TurnedOver = true };
        GameEngine game = Restore(snapshot); GameEngine loaded = GameEngine.Load(game.Save());
        Check(loaded.Human.Health == 0 && loaded.Human.IsAlive && loaded.Human.SpecialCards.Contains(special) && loaded.Human.Chained && loaded.Human.TurnedOver, "Version-three save preserves valid zero-HP Buqu and flip/chain state"); CheckAllCards(loaded);
        snapshot = ExpansionRig("jiangwei", "caocao"); snapshot.Players[0] = snapshot.Players[0] with { Awakened = true, MaxHealth = 4, Health = 4 };
        game = Restore(snapshot); Check(GameEngine.Load(game.Save()).Human.Awakened, "Awakened maximum HP survives save validation");
        snapshot = ExpansionRig("liubei", "caocao", "guanyu", "sunquan") with { Version = 2 };
        void Normal(List<Card> cards) { for (int index = 0; index < cards.Count; index++) cards[index] = cards[index] with { Nature = DamageNature.Normal }; }
        Normal(snapshot.Deck); Normal(snapshot.Discard); Normal(snapshot.Table); foreach (SavedPlayer player in snapshot.Players) { Normal(player.Hand); Normal(player.Equipment); Normal(player.Judgments); }
        loaded = Restore(snapshot); SavedGame migrated = Snapshot(loaded);
        Check(migrated.Version == 5 && migrated.Deck.Concat(migrated.Discard).Concat(migrated.Players.SelectMany(player => player.Hand)).Count(card => card.Nature != DamageNature.Normal) == 8, "Version-two legacy cards migrate canonically to four Fire and four Thunder Slashes");
        snapshot.Players[0] = snapshot.Players[0] with { SkillUsed = true };
        Check(Restore(snapshot).Human.RendeGiven >= 2, "Legacy used Rende cannot heal a second time after migration");
        snapshot = ExpansionRig("xuchu", "caocao") with { Version = 2 };
        Check(Restore(snapshot).Human.LuoyiActive, "Legacy forced human Luoyi remains active when restoring version two");
    }

    private static void VerifyStandardCorrections()
    {
        SavedGame snapshot = ExpansionRig("sunquan", "caocao", "liubei", "guanyu"); Card first = Move(snapshot, CardKind.Dodge, 0); Card second = Move(snapshot, CardKind.Blade, 0); GameEngine game = Restore(snapshot);
        Check(game.UseSkill(null, null), "Standard Zhiheng opens arbitrary hand/equipment selection"); Decision(game, first.Id.ToString()); Decision(game, second.Id.ToString());
        Check(game.Human.Hand.Count == 2 && game.Human.SkillUsed, "Zhiheng replaces exactly all selected cards once");
        snapshot = ExpansionRig("sunshangxiang", "caocao"); snapshot.Players[1] = snapshot.Players[1] with { Health = 2 }; Move(snapshot, CardKind.Dodge, 0); Move(snapshot, CardKind.Peach, 0); game = Restore(snapshot);
        Check(game.UseSkill(null, 1), "Standard Jieyin accepts wounded male target"); FirstDecision(game); FirstDecision(game);
        Check(game.Human.Hand.Count == 0 && game.Players[1].Health == 3, "Standard Jieyin discards two rather than gifting one");
        snapshot = ExpansionRig("zhouyu", "caocao"); Card randomGift = Move(snapshot, CardKind.Dodge, 0); game = Restore(snapshot); Check(game.UseSkill(null, 1), "Standard Fanjian exists and uses a random gift");
        Check(game.Players[1].Hand.Contains(randomGift) && game.Human.SkillUsed, "Fanjian transfers the actual hidden random card");
    }
    private static void VerifyOptionalLossReactions()
    {
        SavedGame snapshot = ExpansionRig("luxun", "caocao"); Card slash = Move(snapshot, CardKind.Slash, 0); GameEngine game = Restore(snapshot);
        Check(game.Play(slash.Id, 1), "Lianying cost action starts"); Decision(game, "no");
        Check(game.Human.Hand.Count == 0 && game.Phase == GamePhase.Play && game.Players[1].Health == snapshot.Players[1].Health - 1, "Declining Lianying resumes attack exactly once without drawing");
        snapshot = ExpansionRig("re_sunshangxiang", "caocao"); Card equipment = Move(snapshot, CardKind.Blade, 0); snapshot.Players[0].Hand.Remove(equipment); snapshot.Players[0].Equipment.Add(equipment);
        game = Restore(snapshot); Call(game, "PayCard", game.Human, equipment); Call(game, "CompleteAction", game.Human); Decision(game, "no");
        Check(game.Human.Hand.Count == 0 && game.DiscardPile.Contains(equipment), "Xiaoji can be declined without reversing the paid equipment loss");
        snapshot = ExpansionRig("luxun", "zhurong"); Card left = Move(snapshot, CardKind.Slash, 1), right = Move(snapshot, CardKind.Dodge, 0); game = Restore(snapshot); bool completed = false;
        Call(game, "CompareHands", game.Players[1], game.Human, (Action<bool>)(_ => completed = true)); Decision(game, right.Id.ToString());
        Check(game.OrderingCards.Count == 2 && !completed && game.PendingDecision?.Prompt.Contains("连营") == true, "Pindian holds both cards in an explicit ordering zone while its loss trigger is pending"); CheckAllCards(game);
        Decision(game, "no");
        Check(completed && game.OrderingCards.Count == 0 && game.DiscardPile.Contains(left) && game.DiscardPile.Contains(right), "Pindian releases both ordering cards only after reactions"); CheckAllCards(game);
    }

    private static void VerifyRebelAlliance()
    {
        GameEngine game = new(42); game.Start("liubei", Role.Lord, GameMode.Identity8);
        List<Player> rebels = game.Players.Where(player => player.Role == Role.Rebel).ToList();
        bool allied = (bool)typeof(GameEngine).GetMethod("Allied", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, new object[] { rebels[0], rebels[1] })!;
        Check(allied, "Identity-mode rebels are allies rather than attacking/rescue-refusing their own faction");
    }

    private static void VerifyLianyingBeforeRetaliation()
    {
        SavedGame snapshot = ExpansionRig("luxun", "xiahoudun");
        snapshot.Players[1] = snapshot.Players[1] with { Role = Role.Rebel };
        snapshot.Players[2] = snapshot.Players[2] with { Role = Role.Loyalist };
        snapshot.Players[3] = snapshot.Players[3] with { Role = Role.Renegade };
        snapshot.Players[0] = snapshot.Players[0] with { Health = 1 };
        Card slash = Move(snapshot, CardKind.Slash, 0);
        PutTop(snapshot, card => card.Suit == "♣"); Card peach = PutTop(snapshot, card => card.Kind == CardKind.Peach);
        GameEngine game = Restore(snapshot); Check(game.Play(slash.Id, 1), "Last-card attack queues Lianying before any retaliatory damage");
        Decision(game, "yes"); Check(game.Human.Hand.Contains(peach), "Lianying drew the Peach before Ganglie judgment and source choice");
        Decision(game, "damage"); Check(game.Pending?.Kind == ResponseKind.Peach, "Retaliatory damage enters real dying after the draw");
        Check(game.Respond(peach.Id), "Newly drawn Peach can save Lu Xun"); Decision(game, "no");
        Check(game.Human.IsAlive && game.Human.Health == 1 && game.Phase == GamePhase.Play, "Nested loss reaction completes before Peach healing and resumes the original action once"); CheckAllCards(game);
    }

}
