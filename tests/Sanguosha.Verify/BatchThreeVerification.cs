using Sanguosha.Core;

namespace Sanguosha.Verify;
internal static partial class Program
{
    private static SavedGame Rig3(string actor, string? victim = null)
    {
        List<string> ids = new() { actor };
        if (victim != null) ids.Add(victim);
        foreach (string candidate in new[] { "gongsunzan", "huaxiong", "zhangfei", "sunquan", "liubei", "zhouyu" })
            if (ids.Count < 4 && !ids.Any(id => (Heroes.Find(id).BaseHeroId ?? id) == candidate)) ids.Add(candidate);
        SavedGame snapshot = ExpansionRig(ids[0], ids[1], ids[2], ids[3]);
        Role[] roles = { Role.Lord, Role.Rebel, Role.Loyalist, Role.Renegade };
        for (int i = 0; i < 4; i++) snapshot.Players[i] = snapshot.Players[i] with { Role = roles[i], Marks = new(), NamedZones = new() };
        return snapshot;
    }
    private static Card Take3(SavedGame snapshot, int seat, Func<Card, bool> predicate)
    {
        Card card = snapshot.Deck.First(predicate); snapshot.Deck.Remove(card); snapshot.Players[seat].Hand.Add(card); return card;
    }
    private static void Settle3(GameEngine game, Func<bool>? completed = null)
    {
        for (int i = 0; i < 1000; i++)
        {
            if (game.PendingDecision is { } choice) Decision(game, choice.Options.FirstOrDefault(option => option.Key is "no" or "done")?.Key ?? choice.Options[0].Key);
            else if (game.PendingCardChoice is { } cards) Check(game.ChooseTargetCard(cards.Options[0].Zone, cards.Options[0].CardId, cards.Options[0].HandPosition), "Resolve actual target-zone selection");
            else if (game.Pending != null) Check(game.Respond(null), "Decline actual response");
            else if (completed?.Invoke() == true || completed == null && game.Phase is GamePhase.Play or GamePhase.GameOver) { CheckAllCards(game); return; }
            else game.Advance();
        }
        throw new Exception("Batch-three resolution did not terminate: " + game.Status);
    }
    private static void VerifyBatchThree()
    {
        VerifyResolutionEdges3(); VerifyGlobalCards3(); VerifyWindFire3(); VerifyForestMountain3(); VerifyStandardSeven3(); VerifyRefreshedTwelve3(); VerifyPersistence3();
        Console.WriteLine("Batch 3: all 29 additions have deterministic skill/interaction coverage; counters, recasts, named zones and extra turns checked.");
    }
    private static void VerifyGlobalCards3()
    {
        SavedGame s = Rig3("sunquan"); Card draw = Move(s, CardKind.DrawTwo, 0), own = Move(s, CardKind.Nullification, 0), enemy = Move(s, CardKind.Nullification, 1);
        GameEngine g = Restore(s); Check(g.Play(draw.Id), "DrawTwo enters use pipeline"); Decision(g, "no"); Decision(g, own.Id.ToString()); Settle3(g);
        Check(g.CardEvents.Count(e => e.Stage == CardUseStage.Counterspell) == 2 && g.Human.Hand.Count == 2, "Counter-counter restores draw-two effect");
        Check(g.DiscardPile.Contains(enemy) && g.DiscardPile.Contains(own), "Both counterspell materials are paid");

        s = Rig3("huangyueying"); Card chain = Move(s, CardKind.IronChain, 0); g = Restore(s); int deck = g.DeckCount;
        Check(g.Play(chain.Id), "Physical chain can be recast"); Decision(g, "recast"); Settle3(g);
        Check(g.DeckCount == deck - 1 && g.CardEvents.Count == 0 && g.Human.Hand.Count == 1, "Recast does not count as use or trigger Jizhi");

        s = Rig3("re_ganning"); chain = Move(s, CardKind.IronChain, 0); g = Restore(s); Check(g.Play(chain.Id), "Chain selects targets before declaration");
        Decision(g, "chain"); Decision(g, "0"); Decision(g, "1"); Decision(g, "0"); Decision(g, "no"); Settle3(g);
        Check(g.Human.LimitedUsed && !g.Human.Chained && g.Players[1].Chained, "Fenwei independently excludes one of two chain targets");
        Check(g.CardEvents.First(e => e.Stage == CardUseStage.Declared).TargetSeats.Count == 2, "Declared frame contains both real targets");

        s = Rig3("xiahoudun"); Card lightning = Move(s, CardKind.Lightning, 0); s.Players[0].Hand.Remove(lightning); s.Players[0].Judgments.Add(lightning); PutTop(s, c => c.Suit == "♠" && c.Rank is >= 2 and <= 9);
        g = Restore(s); List<GameCue> cues = new(); g.CueRaised += cues.Add; bool done = false; int hp = g.Human.Health;
        Call(g, "ResolveJudgments", g.Human, false, false, (Action<bool,bool>)((_,_) => done = true)); Settle3(g, () => done);
        Check(g.Human.Health == hp - 3 && cues.Any(c => c.Kind == GameCueKind.Damaged && c.Amount == 3 && c.SourceSeat == null && c.Nature == DamageNature.Thunder), "Lightning has no damage source and cannot trigger Ganglie retaliation");
        s = Rig3("sunquan"); Card supply = Move(s, CardKind.SupplyShortage, 0); g = Restore(s); Check(g.Play(supply.Id, 1), "Physical Supply Shortage places normally"); Settle3(g);
        Check(!g.Players[1].SupplyShortages.Contains(supply.Id), "Physical supply does not masquerade as Xuhuang converted material"); CheckAllCards(Restore(Snapshot(g)));
    }
    private static void VerifyWindFire3()
    {
        SavedGame s = Rig3("zhangjiao"); Card black = Take3(s, 0, c => !c.IsRed); Card old = PutTop(s, c => c.IsRed); GameEngine g = Restore(s); Card? final = null;
        Call(g, "Judge", g.Players[1], "八卦阵", (Action<Card?>)(c => final = c)); Decision(g, black.Id.ToString());
        Check(final == black && g.Human.Hand.Contains(old) && g.DiscardPile.Contains(black), "Guidao replaces with black card and obtains prior judgment"); CheckAllCards(g);
        s = Rig3("zhangjiao"); PutTop(s, c => c.Suit == "♠"); g = Restore(s); bool done = false; int hp = g.Players[1].Health;
        Call(g, "Leiji", g.Human, (Action)(() => done = true)); Decision(g, "1"); Settle3(g, () => done);
        Check(g.Players[1].Health == hp - 2, "Classic Leiji resolves two thunder damage");
        s = NonLord3("gongsunzan", "zhangjiao"); Card dodge = Move(s, CardKind.Dodge, 0); g = Restore(s);
        Check(g.HasGlobalActive(g.Human) && g.CanActivateSkill(g.Human) && g.UseSkill(null, null), "Non-active general can open attached Huangtian"); Decision(g, "huangtian"); Decision(g, dodge.Id.ToString()); Settle3(g);
        Check(g.Players[1].Hand.Contains(dodge) && !g.HasGlobalActive(g.Human), "Huangtian pays a real card and is once per phase");

        s = Rig3("xunyu"); s.Players[0] = s.Players[0] with { Health = 2 }; Card high = Take3(s, 0, c => c.Rank == 13); Take3(s, 1, c => c.Rank == 1); g = Restore(s); hp = g.Players[2].Health;
        Check(g.UseSkill(null, 1), "Quhu starts comparison with higher-HP target"); Decision(g, high.Id.ToString()); Decision(g, "2"); Settle3(g);
        Check(g.Players[2].Health == hp - 1 && g.Human.SkillUsed, "Quhu winner chooses a third party in opponent range");
        s = Rig3("xunyu"); g = Restore(s); done = false; Call(g, "Damage", g.Players[1], g.Human, null, (Action)(() => done = true), null); Decision(g, "0"); Settle3(g, () => done);
        Check(g.Human.Hand.Count == g.Human.MaxHealth, "Jieming fills selected hand to max HP, capped at five");

        s = Rig3("wolong"); Card red = Take3(s, 0, c => c.Suit == "♥"), cost = Take3(s, 0, c => c.Suit == "♥"); Take3(s, 1, c => c.Suit == "♥"); g = Restore(s); hp = g.Players[1].Health;
        Check(g.UseSkill(null, 1), "Huoji accepts a red hand material"); Decision(g, red.Id.ToString()); Decision(g, cost.Id.ToString()); Settle3(g);
        Check(g.Players[1].Health == hp - 1 && g.DiscardPile.Contains(red) && g.DiscardPile.Contains(cost), "Huoji reveals target hand and pays an additional matching suit for fire damage");
        s = Rig3("wolong"); PutTop(s, c => c.IsRed); g = Restore(s); done = false;
        Call(g, "ResolveSlashDefenseCore", g.Players[1], g.Human, new Card(-1, CardKind.Slash, "♠", 7), (Action)(() => done = true)); Decision(g, "yes"); Settle3(g, () => done);
        Check(g.Human.Health == g.Human.MaxHealth, "Bazhen provides real armor judgment without equipment");
        s = Rig3("wolong"); black = Take3(s, 0, c => !c.IsRed && c.Kind != CardKind.Nullification); Card draw = Move(s, CardKind.DrawTwo, 1); g = Restore(s); done = false;
        Call(g, "ResolveCard", g.Players[1], draw, null, false); Decision(g, black.Id.ToString());
        Check(g.Players[1].Hand.Count == 0 && g.DiscardPile.Contains(black), "Kanpo counters with a paid black hand card"); CheckAllCards(g);

        s = Rig3("yuanshao", "caocao"); Card first = Take3(s, 0, c => c.Suit == "♣"), second = Take3(s, 0, c => c.Suit == "♣"); g = Restore(s);
        Check(g.UseSkill(null, null), "Luanji chooses two matching suits"); Decision(g, first.Id.ToString()); Decision(g, second.Id.ToString()); Settle3(g);
        Check(g.Players[1].Hand.Contains(first) && g.Players[1].Hand.Contains(second), "Jianxiong obtains both AOE materials, not a placeholder or only one card");
        Call(g, "BeginDiscardCore", g.Human); Check(g.Human.HandLimit == g.Human.Health + 2 * g.Players.Count(p => p.IsAlive && p != g.Human && p.Hero.Kingdom == "群"), "Xueyi adds two per other Qun role");
    }
    private static SavedGame NonLord3(string actor, string lord)
    {
        SavedGame s = Rig3(actor, lord);
        s.Players[0] = s.Players[0] with { Role = Role.Rebel, Health = Heroes.Find(actor).Health, MaxHealth = Heroes.Find(actor).Health };
        s.Players[1] = s.Players[1] with { Role = Role.Lord, Health = Heroes.Find(lord).Health + 1, MaxHealth = Heroes.Find(lord).Health + 1 };
        return s;
    }
    private static void VerifyForestMountain3()
    {
        SavedGame s = Rig3("caopi"); GameEngine g = Restore(s); bool done = false;
        Call(g, "Damage", g.Players[1], g.Human, null, (Action)(() => done = true), null); Decision(g, "1"); Settle3(g, () => done);
        Check(g.Players[1].TurnedOver && g.Players[1].Hand.Count == 1, "Fangzhu draws lost HP and toggles face-down state");
        s = Rig3("caopi"); Card loot = Move(s, CardKind.Blade, 1); g = Restore(s); g.Players[1].Health = 0; done = false;
        Call(g, "EliminateWithTriggers", g.Players[2], g.Players[1], (Action)(() => done = true)); Decision(g, "yes"); Settle3(g, () => done);
        Check(!g.Players[1].IsAlive && g.Human.Hand.Contains(loot), "Xingshang takes real deceased hand/equipment before cleanup");
        s = NonLord3("lidian", "caopi"); g = Restore(s); done = false;
        Call(g, "Songwei", g.Human, new Card(-1, CardKind.Slash, "♠", 1), (Action)(() => done = true)); Decision(g, "yes"); Settle3(g, () => done); Check(g.Players[1].Hand.Count == 1, "Songwei lord receives black-judgment tribute");

        s = Rig3("dongzhuo", "huangyueying"); Card spade = Take3(s, 0, c => c.Suit == "♠"); Move(s, CardKind.Dodge, 1); Move(s, CardKind.Dodge, 1); g = Restore(s); int hp = g.Players[1].Health;
        Check(g.UseSkill(null, null), "Jiuchi starts spade-as-wine"); Decision(g, spade.Id.ToString()); Settle3(g); Check(g.Human.Mark("wine") == 1 && g.CardEvents.Any(e => e.Kind == CardKind.Wine), "Jiuchi produces a real Wine declaration");
        done = false; Call(g, "ResolveSlashDefenseCore", g.Human, g.Players[1], new Card(-1, CardKind.Slash, "♠", 1), (Action)(() => done = true)); Settle3(g, () => done);
        Check(g.Players[1].Health == hp && g.Players[1].Hand.Count == 0, "Roulin requires two dodges for female target");
        done = false; Call(g, "BatchThreeEnd", g.Human, (Action)(() => done = true)); Decision(g, "max"); Settle3(g, () => done); Check(g.Human.MaxHealth == 8 && g.Human.Health == 8, "Benghuai can reduce maximum HP and clamp current HP");
        s = Rig3("dongzhuo"); s.Players[0] = s.Players[0] with { Health = 1 }; spade = Take3(s, 0, c => c.Suit == "♠"); g = Restore(s); done = false;
        Call(g, "LoseHealth", g.Human, 1, (Action)(() => done = true)); Check(g.Respond(spade.Id), "Jiuchi spade can save its dying owner"); Settle3(g, () => done); Check(g.Human.Health == 1, "Jiuchi dying Wine actually recovers HP");

        s = Rig3("jiaxu"); g = Restore(s); int[] health = g.Players.Select(p => p.Health).ToArray(); Check(g.UseSkill(null, null), "Limited Luanwu is playable"); Settle3(g);
        Check(g.Human.LimitedUsed && g.Players.Skip(1).All(p => p.Health == health[p.Seat] - 1) && !g.UseSkill(null, null), "Each unarmed role loses HP; Luanwu cannot be reused");
        s = Rig3("sunquan", "jiaxu"); Card black = Take3(s, 0, c => c.Kind == CardKind.Dismantle && !c.IsRed); Move(s, CardKind.Dodge, 1); g = Restore(s); Check(!g.Play(black.Id, 1), "Weimu excludes black trick targets before paying material");
        s = Rig3("jiaxu"); s.Players[1] = s.Players[1] with { Health = 1 }; Card peach = Move(s, CardKind.Peach, 2); g = Restore(s); done = false;
        Call(g, "Damage", g.Human, g.Players[1], null, (Action)(() => done = true), null); Settle3(g, () => done); Check(!g.Players[1].IsAlive && g.Players[2].Hand.Contains(peach), "Wansha prevents a third party from supplying Peach");

        s = Rig3("dengai"); List<Card> fields = s.Deck.Take(3).ToList(); foreach (Card c in fields) s.Deck.Remove(c); s.Players[0] = s.Players[0] with { NamedZones = new() { ["田"] = fields } }; Card targetCard = Move(s, CardKind.Dodge, 1); g = Restore(s); done = false;
        Call(g, "BatchThreePreparation", g.Human, (Action)(() => done = true)); Settle3(g, () => done); Check(g.Human.Awakened && g.Human.MaxHealth == 4, "Three named fields awaken Zaoxian exactly once");
        Check(g.UseSkill(null, 1), "Awakened Jixi can use a field"); FirstDecision(g); Settle3(g); Check(g.Human.Zone("田").Count == 2 && g.Human.Hand.Contains(targetCard), "Jixi spends exactly one named-zone material and steals a selected real card"); CheckAllCards(Restore(Snapshot(g)));
        s = NonLord3("dengai", "sunquan"); Card lost = Move(s, CardKind.Dodge, 0); PutTop(s, c => c.Suit == "♣"); g = Restore(s); typeof(GameEngine).GetProperty("CurrentSeat")!.SetValue(g, 1); Call(g, "PayCard", g.Human, lost); done = false; Call(g, "AfterLosses", (Action)(() => done = true)); Decision(g, "yes"); Settle3(g, () => done); Check(g.Human.Zone("田").Count == 1, "Outside-turn loss triggers optional Tuntian and moves judgment into field zone");

        s = Rig3("liushan"); s.Players[0] = s.Players[0] with { Health = 1 }; g = Restore(s); done = false; Call(g, "BatchThreePreparation", g.Human, (Action)(() => done = true)); Settle3(g, () => done); Check(g.Human.Awakened && g.Human.MaxHealth == 5 && g.Human.Health == 2, "Lord Ruoyu raises max HP, heals, grants Jijiang");
        s = Rig3("liushan"); g = Restore(s); done = false; hp = g.Human.Health; Call(g, "ResolveSlashDefense", g.Players[1], g.Human, new Card(-1, CardKind.Slash, "♠", 1), (Action)(() => done = true)); Settle3(g, () => done); Check(g.Human.Health == hp, "Xiangle cancels Slash when attacker cannot pay basic card");
        s = Rig3("liushan"); Card discard = Move(s, CardKind.Dodge, 0); s.Players[0] = s.Players[0] with { Marks = new() { ["fangquan"] = 1 } }; g = Restore(s); done = false; Call(g, "BatchThreeEnd", g.Human, (Action)(() => done = true)); Decision(g, "2"); Decision(g, discard.Id.ToString()); Settle3(g, () => done); Call(g, "CompleteAction", g.Human);
        SavedGame saved = Snapshot(g); Check(saved.ExtraTurns!.SequenceEqual(new[] { 2 }), "Fangquan stores chosen extra turn"); g = Restore(saved); Call(g, "NextTurn"); Check(g.CurrentSeat == 2, "Queued extra turn runs before normal successor"); Call(g, "NextTurn"); Check(g.CurrentSeat == 1, "After extra turn, normal order resumes at original successor");

        s = Rig3("sunce"); g = Restore(s); done = false; Call(g, "StartVirtualDuel", g.Human, g.Players[1], (Action)(() => done = true), true); Decision(g, "yes"); Settle3(g, () => done); Check(g.Human.Hand.Count == 1, "Jiang reacts to a virtual Duel declaration");
        s = Rig3("sunce"); s.Players[0] = s.Players[0] with { Health = 1 }; g = Restore(s); done = false; Call(g, "BatchThreePreparation", g.Human, (Action)(() => done = true)); Decision(g, "1"); Decision(g, "discard"); Settle3(g, () => done); Check(g.Human.Awakened && g.Human.MaxHealth == 4, "Hunzi grants a working Yinghun in same preparation");
        s = NonLord3("sunquan", "sunce"); Card low = Take3(s, 0, c => c.Rank == 1); Card high = Take3(s, 1, c => c.Rank == 13); g = Restore(s); Check(g.UseSkill(null, null), "Attached Zhiba opens for Wu role"); Decision(g, "zhiba"); Decision(g, low.Id.ToString()); Settle3(g); Check(g.Players[1].Hand.Contains(low) && g.Players[1].Hand.Contains(high), "Zhiba lord obtains both actual comparison cards on non-win");
    }
    private static void VerifyStandardSeven3()
    {
        SavedGame s = Rig3("yuejin"); Card basic = Move(s, CardKind.Dodge, 0); GameEngine g = Restore(s); bool done = false; int hp = g.Players[1].Health;
        Call(g, "BatchThreeEnd", g.Players[1], (Action)(() => done = true)); Decision(g, "yes"); Decision(g, basic.Id.ToString()); Settle3(g, () => done); Check(g.Players[1].Health == hp - 1, "Xiaoguo deals damage only after failed equipment payment");
        s = Rig3("lidian"); g = Restore(s); Call(g, "ResolveDrawPhase", g.Human, false); Decision(g, "yes"); FirstDecision(g); FirstDecision(g); FirstDecision(g); FirstDecision(g); Settle3(g); Check(g.Human.Hand.Count == 2, "Xunxun chooses two of four and orders the remainder at bottom");
        s = Rig3("lidian"); g = Restore(s); done = false; Call(g, "Damage", g.Players[1], g.Human, null, (Action)(() => done = true), null); Decision(g, "yes"); Settle3(g, () => done); Check(g.Human.Hand.Count == 1 && g.Players[1].Hand.Count == 1, "Wangxi draws for both real damage participants");
        s = Rig3("ganfuren"); s.Players[0] = s.Players[0] with { Health = 1 }; Move(s, CardKind.Dodge, 0); g = Restore(s); done = false; Call(g, "BatchThreePreparation", g.Human, (Action)(() => done = true)); Decision(g, "yes"); Decision(g, "1"); Settle3(g, () => done); Check(g.Human.Health == 2 && g.Human.Hand.Count == 0 && g.Players[1].Hand.Count == 2, "Shenzhi discards all; standard Shushen draws two for empty-hand recipient");
        s = Rig3("panfeng"); Card slash = Move(s, CardKind.Slash, 0); g = Restore(s); Check(g.Play(slash.Id, 1), "Panfeng plays actual Slash"); Settle3(g); Check(g.Human.Hand.Count == 2 && g.Human.Mark("panfengUsed") == 1, "First Slash damage with lower-HP victim draws two"); done = false; Call(g, "Damage", g.Human, g.Players[1], null, (Action)(() => done = true), CardKind.Slash); Settle3(g, () => done); Check(g.Human.Hand.Count == 2, "Kuangfu does not trigger twice in phase");
        s = Rig3("gongsunzan"); s.Players[0] = s.Players[0] with { Health = 2 }; g = Restore(s); Check(g.Distance(g.Human, g.Players[2]) == 1 && g.Distance(g.Players[2], g.Human) == 3, "Reyicong outgoing correction and low-HP incoming correction are asymmetric");
        s = Rig3("yiji"); Card bottom = s.Deck[0]; g = Restore(s); Check(g.UseSkill(null, null), "Jijie exposes bottom card only to user"); Decision(g, "1"); Decision(g, "yes"); Settle3(g); Check(g.Players[1].Hand.Contains(bottom) && g.Players[1].Hand.Count == 2 && !g.UseSkill(null, null), "Jijie grants bottom plus optional Jiyuan once per phase");
        s = NonLord3("yuanshu", "sunquan"); s.Players[0] = s.Players[0] with { Health = 2 }; g = Restore(s); done = false; Call(g, "BatchThreePreparation", g.Players[1], (Action)(() => done = true)); Settle3(g, () => done); Check(g.Human.Hand.Count == 2 && g.Players[1].Mark("wangzunPenalty") == 1, "Rewangzun draws twice and lowers higher-HP lord's current-turn hand limit");
        s = NonLord3("sunquan", "yuanshu"); basic = Move(s, CardKind.Dodge, 0); g = Restore(s); Player? redirected = null; Call(g, "BeforeSlashTarget", g.Players[2], g.Human, new Card(-1, CardKind.Slash, "♠", 1), (Action<Player>)(p => redirected = p), (Action)(() => {})); Decision(g, "yes"); Decision(g, basic.Id.ToString()); Check(redirected == g.Players[1] && g.DiscardPile.Contains(basic), "Retongji pays original target's cost and redirects to Yuan Shu"); CheckAllCards(g);
    }
    private static void VerifyRefreshedTwelve3()
    {
        SavedGame s = Rig3("re_huatuo"); for (int i = 0; i < 3; i++) s.Players[i] = s.Players[i] with { Health = s.Players[i].MaxHealth - 1 };
        Card red = Take3(s, 0, c => c.IsRed), red2 = Take3(s, 0, c => c.IsRed), black = Take3(s, 0, c => !c.IsRed); GameEngine g = Restore(s);
        Check(g.UseSkill(red.Id, 0) && !g.UseSkill(red2.Id, 0), "Reqingnang heals each target once"); Check(g.UseSkill(red2.Id, 1) && g.UseSkill(black.Id, 2), "Red costs allow another target; black cost resolves final heal"); Check(g.Human.Mark("qingnangOff") == 1 && g.Players[2].Health == g.Players[2].MaxHealth, "Black cost disables later Qingnang for this turn"); CheckAllCards(g);
        s = Rig3("re_daqiao"); Card diamond = Take3(s, 0, c => c.Suit == "♦"); g = Restore(s); Check(g.UseSkill(null, 1), "Reguose supports real Indulgence"); Decision(g, diamond.Id.ToString()); Settle3(g); Check(g.Players[1].Judgments.Contains(diamond) && g.Human.Hand.Count == 1, "Reguose places converted material then draws one"); CheckAllCards(Restore(Snapshot(g)));
        s = Rig3("re_daqiao"); diamond = Take3(s, 0, c => c.Suit == "♦" && c.IsEquipment); s.Players[0].Hand.Remove(diamond); s.Players[0].Equipment.Add(diamond); Card lebu = Move(s, CardKind.Indulgence, 1); s.Players[1].Hand.Remove(lebu); s.Players[1].Judgments.Add(lebu); g = Restore(s); Check(g.UseSkill(null, 1), "Reguose accepts a diamond equipment cost"); Decision(g, diamond.Id.ToString()); Settle3(g); Check(g.Players[1].Judgments.Count == 0 && g.DiscardPile.Contains(lebu), "Reguose removes existing Indulgence without silently creating another");
        s = Rig3("re_zhangliao"); Card stolen = Move(s, CardKind.Dodge, 1); g = Restore(s); Call(g, "ResolveDrawPhase", g.Human, false); Decision(g, "1"); Settle3(g); Check(g.Human.Hand.Contains(stolen) && g.Human.Hand.Count == 2, "Retuxi one target leaves one normal draw");
        s = Rig3("re_guojia"); Card old = Move(s, CardKind.Dodge, 0); g = Restore(s); bool done = false; Call(g, "Damage", g.Players[1], g.Human, null, (Action)(() => done = true), null); Decision(g, "yes"); Decision(g, old.Id.ToString()); Decision(g, "1"); Decision(g, "done"); Settle3(g, () => done); Check(g.Players[1].Hand.Contains(old) && g.Human.Hand.Count == 2, "Reyiji can distribute a pre-existing hand card, not only just-drawn cards");
        s = Rig3("re_zhugeliang"); g = Restore(s); done = false; Call(g, "Guanxing", g.Human, (Action)(() => done = true)); for (int i = 0; i < 5; i++) Decision(g, g.PendingDecision!.Options.First(o => o.Key.StartsWith("bottom:")).Key); Settle3(g, () => done); Check(g.Human.Mark("guanxingBottom") == 1, "Reguanxing at four players sees five and tracks all-bottom end trigger");
        s = Rig3("re_huangyueying"); Card draw = Move(s, CardKind.DrawTwo, 0); Card drawn = PutTop(s, c => c.IsBasic); g = Restore(s); Check(g.Play(draw.Id), "Rejizhi uses full trick declaration"); Decision(g, "yes"); Decision(g, "yes"); Settle3(g); Check(g.DiscardPile.Contains(drawn) && g.Human.Mark("handBonus") == 1 && g.Human.Hand.Count == 2, "Rejizhi discards the exact drawn basic for temporary hand limit");
        s = Rig3("sunquan", "re_huangyueying"); Card dismantle = Move(s, CardKind.Dismantle, 0), steal = Move(s, CardKind.Steal, 0), armor = Move(s, CardKind.EightTrigrams, 1); s.Players[1].Hand.Remove(armor); s.Players[1].Equipment.Add(armor); g = Restore(s); Check(!g.Play(dismantle.Id, 1) && g.Play(steal.Id, 1), "Reqicai armor cannot be discarded by another, but remains stealable"); Settle3(g); Check(g.Human.Hand.Contains(armor), "Protected armor theft transfers actual equipment");
        s = Rig3("re_liubei"); s.Players[0] = s.Players[0] with { Health = 2 }; Card a = Move(s, CardKind.Dodge, 0), b = Move(s, CardKind.Dodge, 0); g = Restore(s); Check(g.UseSkill(a.Id, 1), "Rerende chooses a real batch gift"); Decision(g, a.Id.ToString()); Decision(g, b.Id.ToString()); Decision(g, "peach"); Settle3(g); Check(g.Human.Health == 3 && g.Players[1].Hand.Contains(a) && g.Players[1].Hand.Contains(b), "Two-card Rerende grants actual virtual Peach, not automatic two-point heal");
        s = Rig3("re_liubei"); s.Players[2] = s.Players[2] with { HeroId = "guanyu", MaxHealth = 4, Health = 4 }; g = Restore(s); done = false; Call(g, "JijiangDraw", g.Players[2], (Action)(() => done = true)); Settle3(g, () => done); Check(g.Human.Hand.Count == 1, "Rejijiang draws for allied Shu outside-turn Slash");
        s = Rig3("re_sunquan"); a = Move(s, CardKind.Dodge, 0); b = Move(s, CardKind.Peach, 0); g = Restore(s); Check(g.UseSkill(null, null), "Rezhiheng selects all hand"); Decision(g, a.Id.ToString()); Decision(g, b.Id.ToString()); Settle3(g); Check(g.Human.Hand.Count == 3, "Rezhiheng full-hand cost draws one bonus");
        s = NonLord3("zhouyu", "re_sunquan"); s.Players[0] = s.Players[0] with { Health = 2 }; s.Players[1] = s.Players[1] with { Health = 1 }; g = Restore(s); done = false; Call(g, "HealWithChoices", g.Human, (Action)(() => done = true)); Decision(g, "yes"); Settle3(g, () => done); Check(g.Human.Health == 2 && g.Players[1].Health == 2 && g.Human.Hand.Count == 1, "Rejiuyuan redirects own-turn recovery to Wu lord and compensates drawer");
        s = Rig3("re_zhaoyun"); Card peach = Move(s, CardKind.Peach, 0); g = Restore(s); Check(g.Play(peach.Id, null, true), "Ollongdan Peach-to-Wine is playable"); Settle3(g); Check(g.Human.Mark("wine") == 1, "Converted Peach resolves as Wine");
        s = NonLord3("re_zhaoyun", "sunquan"); a = Move(s, CardKind.Dodge, 0); Card top = PutTop(s, c => c.IsBasic); g = Restore(s); typeof(GameEngine).GetProperty("CurrentSeat")!.SetValue(g, 1); done = false; Call(g, "Request", g.Human, ResponseKind.Dodge, "test", (Action<bool>)(_ => done = true)); Check(g.Respond(a.Id), "Physical response triggers Yajiao"); Decision(g, "yes"); Decision(g, "0"); Settle3(g, () => done); Check(g.Human.Hand.Contains(top), "Yajiao matching category grants the shown top card");
        s = Rig3("re_zhangfei"); a = Move(s, CardKind.Slash, 0); b = Move(s, CardKind.Slash, 0); Move(s, CardKind.Dodge, 1); g = Restore(s); Check(g.Play(a.Id, 1), "Olpaoxiao first Slash"); Settle3(g); Check(g.Human.Mark("paoxiaoBoost") == 1, "Dodged Slash stores boost"); int hp = g.Players[1].Health; Check(g.Play(b.Id, 1), "Olpaoxiao allows second Slash"); Settle3(g); Check(g.Players[1].Health == hp - 2 && g.Human.Mark("paoxiaoBoost") == 0, "Next Slash damage consumes accumulated dodge boost");
        s = Rig3("re_zhangfei"); s.Players[0] = s.Players[0] with { Health = 2 }; g = Restore(s); done = false; Call(g, "BatchThreePreparation", g.Human, (Action)(() => done = true)); Decision(g, "yes"); Settle3(g, () => done); Check(g.Human.LimitedUsed && g.Human.Health == 5 && g.Human.Hand.Count == 3, "Oltishen heals to full and draws actual recovered amount once");
        s = Rig3("re_lvmeng"); for (int i = 0; i < 7; i++) Move(s, CardKind.Slash, 0); g = Restore(s); done = false; Call(g, "BatchThreePreparation", g.Human, (Action)(() => done = true)); Decision(g, "draw"); Settle3(g, () => done); Check(g.Human.Awakened && g.Human.MaxHealth == 4 && g.Human.Hand.Count == 9, "Qinxue awakens at HP+2, loses max HP and draws two");
        s = Rig3("re_lvmeng"); s.Players[0] = s.Players[0] with { Awakened = true, MaxHealth = 4, Health = 4 }; Card heart = Take3(s, 1, c => c.Suit == "♥"); g = Restore(s); Check(g.UseSkill(null, 1), "Awakened Gongxin views hand"); Decision(g, heart.Id.ToString()); Decision(g, "top"); Settle3(g); Check(!g.Players[1].Hand.Contains(heart) && Snapshot(g).Deck.Last() == heart, "Gongxin places chosen heart on actual deck top");
        s = Rig3("re_lvmeng") with { DiscardedSuits = new() { "♠", "♥", "♣", "♦" } }; g = Restore(s);
        for (int i = 0; i < 4; i++) { done = false; Call(g, "BatchThreeEnd", g.Human, (Action)(() => done = true)); if (i < 3) Decision(g, "yes"); Settle3(g, () => done); }
        Call(g, "CompleteAction", g.Human); Check(Snapshot(g).ExtraTurns!.Count == 3, "Rebotu sees all four turn-discard suits and enforces at most three extra turns per round");
    }
    private static void VerifyResolutionEdges3()
    {
        SavedGame s = Rig3("sunquan"); Card trick = Move(s, CardKind.DrawTwo, 0); s.Players[2].Hand.AddRange(s.Deck); s.Deck.Clear();
        GameEngine g = Restore(s); Check(g.Play(trick.Id), "Use the only recyclable card with an empty deck"); Settle3(g);
        Check(!g.Human.Hand.Contains(trick) && g.DiscardPile.Contains(trick), "An unresolved use cannot shuffle its own material back into hand");
        s = Rig3("re_lvmeng"); for (int i = 0; i < 6; i++) Move(s, CardKind.Slash, 0); g = Restore(s); Call(g, "BeginDiscard", g.Human); Decision(g, "no");
        Check(g.Phase == GamePhase.Discard, "Keji can be declined to enter real discard phase (including Botu strategy)");
        s = Rig3("yuanshao"); g = Restore(s); int limit = g.Human.HandLimit; g.Players[1].IsAlive = false;
        Check(g.Human.HandLimit == limit - 2, "Xueyi hand limit updates immediately when a Qun character dies, not just on discard entry");
        s = Rig3("re_huatuo"); s.Players[0] = s.Players[0] with { SkillsDisabled = true, Health = 2 }; Card red = Take3(s, 0, c => c.IsRed); g = Restore(s);
        Check(!g.UseSkill(red.Id, 0) && !g.CanActivateSkill(g.Human), "Duanchang suppresses new refreshed active skills, not just baseline aliases");
        s = Rig3("sunquan", "dongzhuo"); s.Players[1] = s.Players[1] with { Health = 0, MaxHealth = 0, IsAlive = false }; g = Restore(s); Check(!g.Players[1].IsAlive && g.Players[1].MaxHealth == 0, "Benghuai maximum-HP death remains a loadable saved state");
        s = Rig3("jiaxu"); Card blackDraw = Take3(s, 0, c => c.Kind == CardKind.DrawTwo && !c.IsRed); g = Restore(s); Check(!g.Play(blackDraw.Id) && g.Human.Hand.Contains(blackDraw), "Weimu cannot illegally self-target with a black custom-deck DrawTwo");
        s = Rig3("re_zhaoyun"); s.Players[0] = s.Players[0] with { Health = 2 }; Card wine = Move(s, CardKind.Wine, 0); g = Restore(s);
        Check(g.Play(wine.Id, null, true), "Longdan can also convert Wine back into Peach"); Settle3(g); Check(g.Human.Health == 3, "Converted Wine heals as Peach");
    }
    private static void VerifyPersistence3()
    {
        SavedGame s = Rig3("dengai"); List<Card> fields = s.Deck.Take(3).ToList(); foreach (Card c in fields) s.Deck.Remove(c); s.Players[0] = s.Players[0] with { NamedZones = new() { ["田"] = fields }, Marks = new() { ["sample"] = 2 } }; s = s with { ExtraTurns = new() { 2 }, ExtraTurnOrigin = 1, DiscardedSuits = new() { "♠", "♥" } };
        GameEngine g = Restore(s); SavedGame saved = Snapshot(g); Check(saved.Version == 5 && saved.Players[0].NamedZones!["田"].SequenceEqual(fields) && saved.ExtraTurns!.SequenceEqual(new[] { 2 }) && saved.ExtraTurnOrigin == 1 && saved.DiscardedSuits!.Count == 2, "V4 round-trips named piles, marks, extra-turn origin/queue and discarded suits"); CheckAllCards(g);
        foreach (int version in new[] { 2, 3 })
        {
            s = Rig3("sunquan") with { Version = version }; s.Deck.RemoveAll(c => c.Id > 108); s.Discard.RemoveAll(c => c.Id > 108); s.Table.RemoveAll(c => c.Id > 108);
            foreach (SavedPlayer p in s.Players) { p.Hand.RemoveAll(c => c.Id > 108); p.Equipment.RemoveAll(c => c.Id > 108); p.Judgments.RemoveAll(c => c.Id > 108); }
            g = Restore(s); Check(g.PhysicalCardCount == 108 && Snapshot(g).Version == 5, "Legacy 108-card save migrates without adding new physical cards"); CheckAllCards(g);
        }
    }
}
