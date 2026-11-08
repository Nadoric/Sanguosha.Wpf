using System.Text.Json;
using Sanguosha.Core;
namespace Sanguosha.Verify;
internal static partial class Program
{
    private static void VerifyCoreCompletion()
    {
        foreach (string pack in new[] { "神话·风", "神话·火", "神话·林", "神话·山" }) Check(Heroes.All.Count(hero => hero.Pack == pack) == 8, pack + " complete 8/8");
        Check(Heroes.All.Count(hero => hero.BaseHeroId != null) == 25, "Refreshed original standard roster complete 25/25");
        VerifySeals6(); VerifyTieji6(); VerifyGanglieQingjian6(); VerifyQianxun6(); VerifyGuhuo6(); VerifyIncarnation6(); VerifyBorrowedTriggers6();
        Console.WriteLine("Batch 4 / patch6: all six final generals, category bans, private/batch zones, classic Guhuo and independent incarnation skills checked.");
    }
    private static void VerifySeals6()
    {
        SavedGame s = Rig3("re_guanyu", "zhaoyun"); Card cost = Move(s, CardKind.Dodge, 0), heart = Take3(s, 0, c => c.Kind == CardKind.Slash && c.Suit == "♥"); Card reveal = Take3(s, 1, c => c.Kind == CardKind.Dodge && !c.IsRed);
        GameEngine g = Restore(s); Check(g.UseSkill(cost.Id, 1), "Yijue opens target/cost interaction"); Decision(g, "1"); Decision(g, cost.Id.ToString()); Settle3(g);
        Check(g.Players[1].HandUseBanned && !g.Players[1].HasAbility("龙胆") && !g.CanTransform(g.Players[1], reveal, ResponseKind.Dodge), "Black Yijue seals ordinary skills and even natural hand responses");
        int hp = g.Players[1].Health; Check(g.Play(heart.Id, 1), "Heart Slash after Yijue"); Settle3(g); Check(g.Players[1].Health == hp - 2 && g.Players[1].Hand.Contains(reveal), "Yijue heart Slash gets exactly +1 and cannot be hand-dodged");
        GameEngine loaded = Restore(Snapshot(g)); Check(loaded.Players[1].HandUseBanned && loaded.Players[1].TemporarySkillBans.Contains(SkillCategory.Ordinary), "Temporary restrictions survive V5 save");
        bool done = false; Call(g, "FinishGlobalTurn", (Action)(() => done = true)); Settle3(g, () => done); Check(!g.Players[1].HandUseBanned && g.Players[1].HasAbility("龙胆"), "Seals expire at current global turn end");
        s = Rig3("re_guanyu", "guojia"); cost = Move(s, CardKind.Dodge, 0); reveal = Take3(s, 1, c => c.IsRed); s.Players[1] = s.Players[1] with { Health = 2 }; g = Restore(s);
        Check(g.UseSkill(cost.Id, 1), "Red Yijue branch"); Decision(g, "1"); Decision(g, cost.Id.ToString()); Decision(g, "yes"); Settle3(g);
        Check(g.Human.Hand.Contains(reveal) && !g.Players[1].Hand.Contains(reveal) && g.Players[1].Health == 3 && !g.Players[1].HandUseBanned, "Red reveal is actually transferred and optional heal resolves");
        s = Rig3("re_guanyu"); Card diamond = Take3(s, 0, c => c.Kind == CardKind.Slash && c.Suit == "♦"); g = Restore(s);
        Check(g.Distance(g.Human, g.Players[2]) > g.Human.AttackRange && g.ValidatePlay(g.Human, diamond, g.Players[2]) == null, "Natural diamond Slash ignores range with refreshed Wusheng");
        g.Human.TemporarySkillBans.Add(SkillCategory.Ordinary); Check(g.ValidatePlay(g.Human, diamond, g.Players[2]) != null && !g.CanUseConversion(g.Human, cost), "Banned Wusheng loses conversion and diamond range modifier");
        foreach (string id in new[] { "re_machao", "re_lvbu", "re_zhouyu", "huangyueying", "xiaoqiao", "jiaxu", "yuanshao", "sunquan" })
        {
            Player p = new() { Hero = Heroes.Find(id), Health = 3, MaxHealth = 4 }; p.TemporarySkillBans.UnionWith(new[] { SkillCategory.Ordinary, SkillCategory.Lord, SkillCategory.Limited });
            foreach (var skill in SkillCatalog.For(p.Hero)) Check(p.HasAbility(skill.Name) == (skill.Category is SkillCategory.Locked or SkillCategory.Awakening or SkillCategory.LockedLord), "Correct ordinary/locked/lord/limited category under seal: " + id + skill.Name);
        }
    }
    private static void VerifyTieji6()
    {
        SavedGame s = Rig3("re_machao", "zhaoyun"); Card slash = Move(s, CardKind.Slash, 0); Card dodge = Take3(s, 1, c => c.Kind == CardKind.Dodge && c.Suit == "♠"); PutTop(s, c => c.Suit == "♦"); GameEngine g = Restore(s); int hp = g.Players[1].Health;
        Check(g.Play(slash.Id, 1), "Refreshed Tieji Slash"); Decision(g, "yes"); Settle3(g);
        Check(g.Players[1].Health == hp - 2 && g.Players[1].Hand.Contains(dodge) && !g.Players[1].HasAbility("龙胆") && !g.Players[1].HandUseBanned, "No matching gift: direct hit +1, ordinary seal but no general hand ban");
        s = Rig3("re_machao", "zhaoyun"); slash = Move(s, CardKind.Slash, 0); Card matched = Take3(s, 1, c => c.Kind == CardKind.Dodge && c.Suit == "♦"); Card second = Take3(s, 1, c => c.Kind == CardKind.Dodge && c.Suit == "♠"); PutTop(s, c => c.Suit == "♦"); g = Restore(s); hp = g.Players[1].Health;
        Check(g.Play(slash.Id, 1), "Tieji matching-suit gift branch"); Decision(g, "yes"); Settle3(g);
        Check(g.Human.Hand.Contains(matched) && !g.DiscardPile.Contains(matched) && !g.Players[1].Hand.Contains(second) && g.Players[1].Health == hp, "Matching card is GIVEN, not discarded; a second natural Dodge remains legal");
        Check(g.Distance(g.Human, g.Players[2]) == 1 && g.Human.HasAbility("马术"), "Locked Mashu works independently from ordinary Tieji");
    }
    private static void VerifyGanglieQingjian6()
    {
        SavedGame s = Rig3("re_xiahoudun", "sunquan"); Card victimCard = Move(s, CardKind.Dodge, 1); PutTop(s, c => !c.IsRed); PutTop(s, c => c.IsRed); GameEngine g = Restore(s); bool done = false; int hp = g.Players[1].Health;
        Call(g, "ElementalDamage", g.Players[1], g.Human, null, 2, DamageNature.Normal, (Action)(() => done = true)); Decision(g, "yes"); Decision(g, "yes"); Settle3(g, () => done);
        Check(g.Players[1].Health == hp - 1 && g.DiscardPile.Contains(victimCard), "Reganglie independently judges each damage point: red damage, black HE discard");
        s = Rig3("re_xiahoudun", "guanyu"); Card basic = Move(s, CardKind.Slash, 0), trick = Move(s, CardKind.Dismantle, 0), equip = Move(s, CardKind.Blade, 0); s.Players[0].Hand.Remove(equip); s.Players[0].Equipment.Add(equip); g = Restore(s);
        Call(g, "Draw", g.Human, 2); done = false; Call(g, "AfterLosses", (Action)(() => done = true)); Decision(g, "1"); Decision(g, basic.Id.ToString()); Decision(g, trick.Id.ToString()); Decision(g, equip.Id.ToString()); Decision(g, "done"); Settle3(g, () => done);
        Check(g.Human.Mark("qingjianUsed") == 1 && g.Human.HandLimit == g.Human.Health + 3 && g.Players[1].Hand.Contains(equip) && g.Players[1].Hand.Contains(basic) && g.Players[1].Hand.Contains(trick), "New Qingjian grants real batch HE gift and three type2 hand-limit bonus, once per global turn");
        Call(g, "Draw", g.Human, 1); done = false; Call(g, "AfterLosses", (Action)(() => done = true)); Check(done && g.PendingDecision == null, "Qingjian cannot repeat in the same global turn");
        done = false; Call(g, "FinishGlobalTurn", (Action)(() => done = true)); Settle3(g, () => done); Check(g.Human.Mark("qingjianUsed") == 0 && g.Human.HandLimit == g.Human.Health, "Qingjian limit and usage expire for everyone, not recipient's next turn");
        s = Rig3("re_xiahoudun"); g = Restore(s); Call(g, "ResolveDrawPhase", g.Human, false); Check(g.Phase == GamePhase.Play && g.PendingDecision == null && g.Human.Mark("qingjianUsed") == 0, "Own normal draw phase does not offer Qingjian");
    }
    private static void VerifyQianxun6()
    {
        SavedGame s = Rig3("re_luxun"); Card a = Move(s, CardKind.Dodge, 0), b = Move(s, CardKind.Peach, 0), c = Move(s, CardKind.Blade, 0); GameEngine g = Restore(s); bool done = false;
        Call(g, "Qianxun", g.Human, (Action)(() => done = true)); Decision(g, "yes"); Decision(g, "0"); Decision(g, "1"); Decision(g, "2"); Settle3(g, () => done);
        Check(g.Human.Zone("谦逊").Count == 3 && g.Human.Hand.Count == 1 && g.Players[1].Hand.Count == 1 && g.Players[2].Hand.Count == 1, "One three-card loss gives three DISTINCT recipients exactly one draw, with protected stored cards");
        Check(g.HandEvents.Any(e => !e.Gained && e.Seat == 0 && e.CardIds.Count == 3), "Batch loss records actual three physical cards");
        Call(g, "CompleteAction", g.Human); g = Restore(Snapshot(g)); g.Human.SkillsDisabled = true; done = false; Call(g, "FinishGlobalTurn", (Action)(() => done = true)); Settle3(g, () => done);
        Check(g.Human.Zone("谦逊").Count == 0 && new[] { a,b,c }.All(g.Human.Hand.Contains) && g.Human.Hand.Count == 4, "All private Qianxun cards return in a batch even after permanent skill loss and save/load");
        s = Rig3("re_luxun"); Card delayed = Move(s, CardKind.Indulgence, 0); s.Players[0].Hand.Remove(delayed); s.Players[0].Judgments.Add(delayed); a = Move(s, CardKind.Dodge, 0); PutTop(s, card => card.Suit == "♥"); g = Restore(s); done = false;
        Call(g, "ResolveJudgments", g.Human, false, false, (Action<bool,bool>)((_,_) => done = true)); Check(g.PendingDecision?.Prompt.Contains("界谦逊") == true, "Delayed-card judgment offers Qianxun before resolving judgment"); Decision(g, "no"); Settle3(g, () => done);
    }
    private static void OnlyTwo6(GameEngine g) { foreach (Player p in g.Players.Skip(2)) { p.IsAlive = false; p.Health = 0; } }
    private static void VerifyGuhuo6()
    {
        foreach ((bool truth, string suit, bool question, bool expected) in new[] { (true,"♥",true,true), (true,"♠",true,false), (false,"♥",true,false), (false,"♠",false,true) })
        {
            SavedGame s = Rig3("sunquan", "yuji"); Card material = Take3(s, 1, card => card.Suit == suit && (truth ? card.Kind == CardKind.Slash && card.Nature == DamageNature.Normal : card.Kind == CardKind.Dodge));
            GameEngine g = Restore(s); OnlyTwo6(g); int hp = g.Human.Health, hand = g.Human.Hand.Count; bool done = false;
            Call(g, "PlayGuhuo", g.Players[1], material, new Card(0, CardKind.Slash, "", 0), new List<Player> { g.Human }, (Action)(() => done = true));
            Check(g.OrderingCards.Contains(material) && !g.TableCards.Contains(material) && !g.CanSave, "Guhuo material stays face-down and reserved during questioning");
            Decision(g, question ? "question" : "trust"); Settle3(g, () => done);
            Check(g.CardEvents.Any(e => e.Stage == CardUseStage.Declared && e.Kind == CardKind.Slash) == expected, $"Classic Guhuo resolution truth={truth}, suit={suit}, question={question}");
            Check(g.Human.Health == hp - (question && truth ? 1 : 0) - (expected ? 1 : 0), "Questioning true cards loses HP even when non-heart declaration is canceled");
            if (question && !truth) Check(g.Human.Hand.Count == hand + 1, "False questioned declaration lets challenger draw one");
            Check(g.DiscardPile.Contains(material), "Successful or failed Guhuo always pays the actual physical card"); CheckAllCards(g);
        }
        {
            SavedGame multiple = Rig3("sunquan", "yuji"); Card trueHeart = Take3(multiple, 1, card => card.Kind == CardKind.Slash && card.Suit == "♥"); GameEngine many = Restore(multiple); int[] hp = many.Players.Select(p => p.Health).ToArray(); bool done = false;
            Call(many, "PlayGuhuo", many.Players[1], trueHeart, new Card(0, CardKind.Slash, "", 0), new List<Player> { many.Human }, (Action)(() => done = true)); Decision(many, "question"); Settle3(many, () => done);
            Check(many.Logs.Count(log => log.Text.EndsWith("质疑蛊惑。") && !log.Text.EndsWith("不质疑蛊惑。")) == 3 && many.Players[2].Health == hp[2] - 1 && many.Players[3].Health == hp[3] - 1, "Classic Guhuo collects ALL challengers, not only the first one");
        }
        SavedGame state = Rig3("yuji", "sunquan"); Card cost = Take3(state, 0, card => card.Kind == CardKind.Slash && card.Suit == "♥"); GameEngine game = Restore(state); OnlyTwo6(game); bool success = false;
        Call(game, "Request", game.Human, ResponseKind.Dodge, "test Guhuo Dodge", (Action<bool>)(value => success = value)); Decision(game, "Dodge:Normal"); Decision(game, cost.Id.ToString()); Settle3(game, () => success); Check(success && game.DiscardPile.Contains(cost), "Guhuo supplies a real non-natural Dodge response");
        state = Rig3("yuji", "sunquan"); cost = Move(state, CardKind.Nullification, 0); game = Restore(state); OnlyTwo6(game); bool negated = false;
        Call(game, "GuhuoCounterspell", game.Human, game.Players[1], CardKind.DrawTwo, game.Players[1], false, 0, 0, (Action<bool>)(value => negated = value)); Decision(game, cost.Id.ToString()); Settle3(game, () => negated);
        Check(negated && game.CardEvents.Any(e => e.Stage == CardUseStage.Counterspell && e.MaterialIds.Contains(cost.Id)), "Guhuo Nullification enters the actual counter-counter pipeline with real material");
        state = Rig3("yuji", "sunquan"); cost = Take3(state, 0, card => card.Kind == CardKind.Peach && card.Suit == "♥"); game = Restore(state); OnlyTwo6(game); game.Human.Health = 0; bool rescued = false;
        Call(game, "StartRescue", game.Players[1], game.Human, (Action)(() => rescued = true)); Decision(game, "Peach:Normal"); Decision(game, cost.Id.ToString()); Settle3(game, () => rescued); Check(game.Human.Health == 1 && game.Human.IsAlive, "Guhuo can declare Peach while dying and genuinely rescues self");
        game = Restore(state); game.Human.HandUseBanned = true; Call(game, "Request", game.Human, ResponseKind.Dodge, "hand ban", (Action<bool>)(_ => { })); Check(game.PendingDecision == null && game.Pending != null, "Hand ban removes all Guhuo response routes"); Check(!game.Respond(cost.Id), "Banned hand cannot be directly submitted through Respond API");
    }
    private static SavedGame IncarnationRig6(string id, string skill)
    {
        SavedGame s = Rig3("zuoci");
        // All fixtures explicitly define private pool and a legal single skill; unused on-table forms are never borrowed.
        s.Players[0] = s.Players[0] with { Incarnations = new() { id }, IncarnationHeroId = id, IncarnationSkill = skill };
        return s;
    }
    private static void VerifyIncarnation6()
    {
        foreach (Hero hero in Heroes.All.Where(hero => hero.Id != "zuoci"))
        foreach (SkillDefinition skill in SkillCatalog.Borrowable(hero))
        {
            Player p = new() { Hero = Heroes.Find("zuoci"), Health = 3, MaxHealth = 3, IncarnationHeroId = hero.Id, IncarnationSkill = skill.Name }; p.Incarnations.Add(hero.Id);
            Check(p.Kingdom == hero.Kingdom && p.IsFemale == hero.IsFemale, "Incarnation sex/faction follows selected form: " + hero.Id);
            foreach (SkillDefinition other in SkillCatalog.For(hero)) Check(p.HasAbility(other.Name) == (other.Name == skill.Name), "EXACTLY one borrowed named skill, not whole kit: " + hero.Id + "/" + skill.Name + "/" + other.Name);
            p.TemporarySkillBans.Add(SkillCategory.Ordinary); Check(p.HasAbility(skill.Name) == (skill.Category == SkillCategory.Locked), "Incarnation obeys category seals: " + hero.Id + skill.Name);
        }
        GameEngine g = Fresh("zuoci"); Check(g.Human.Incarnations.Count == 2 && g.Human.IncarnationSkill != null, "Classic Huashen starts with exactly two legal random forms");
        Check(g.Human.Incarnations.Select(id => Heroes.Find(id).BaseHeroId ?? id).Distinct().Count() == 2 && g.Human.Incarnations.All(id => !g.Players.Any(p => (p.Hero.BaseHeroId ?? p.Hero.Id) == (Heroes.Find(id).BaseHeroId ?? id))), "Form pool excludes on-table generals and duplicate historical persons");
        SavedGame s = IncarnationRig6("daqiao", "流离"); g = Restore(s); Check(!g.CanActivateSkill(g.Human), "Borrowing Liuli does not grant active Guose");
        s = IncarnationRig6("liubei", "仁德"); g = Restore(s); Check(!g.UseSkill(null, 1), "Borrowing Rende as a lord never grants forbidden Jijiang");
        s = IncarnationRig6("huangyueying", "集智"); Card steal = Move(s, CardKind.Steal, 0); Card held = Move(s, CardKind.Dodge, 2); g = Restore(s); Check(g.ValidatePlay(g.Human, steal, g.Players[2]) != null, "Borrowed Jizhi does NOT accidentally grant locked Qicai distance modifier");
        s = IncarnationRig6("huangyueying", "奇才"); steal = Move(s, CardKind.Steal, 0); held = Move(s, CardKind.Dodge, 2); g = Restore(s); Check(g.ValidatePlay(g.Human, steal, g.Players[2]) == null && !g.Human.HasAbility("集智"), "Borrowing Qicai grants only its range modifier, not Jizhi");
        s = IncarnationRig6("caocao", "奸雄"); s.Players[0] = s.Players[0] with { Incarnations = new() { "caocao", "zhenji" } }; g = Restore(s); bool done = false; Call(g, "ChooseIncarnation", g.Human, true, (Action)(() => done = true)); Decision(g, "zhenji"); Decision(g, "倾国"); Check(done && g.Human.IsFemale && g.Human.Kingdom == "魏" && !g.Human.HasAbility("奸雄") && !g.Human.HasAbility("洛神"), "Switching form changes sex/faction and replaces, not stacks, a single skill");
        Call(g, "CompleteAction", g.Human); GameEngine loaded = Restore(Snapshot(g)); Check(loaded.Human.IncarnationSkill == "倾国" && loaded.Human.IsFemale && loaded.Human.Incarnations.Count == 2, "V5 round-trips private form list and selected skill");
        s = IncarnationRig6("zhoutai", "不屈"); Card pile = s.Deck[0]; s.Deck.RemoveAt(0); s.Players[0] = s.Players[0] with { Health = 0, SpecialCards = new() { pile }, Incarnations = new() { "zhoutai", "caocao" } }; g = Restore(s); done = false; Call(g, "SetIncarnation", g.Human, "caocao", "奸雄", (Action)(() => done = true)); Settle3(g, () => done || g.Phase == GamePhase.GameOver); Check(!g.Human.IsAlive && g.DiscardPile.Contains(pile), "Losing borrowed Buqu discards its pile and genuinely re-enters dying/death");
        s = IncarnationRig6("dengai", "屯田"); pile = s.Deck[0]; s.Deck.RemoveAt(0); s.Players[0] = s.Players[0] with { NamedZones = new() { ["田"] = new() { pile } }, Incarnations = new() { "dengai", "caocao" } }; g = Restore(s); Call(g, "SetIncarnation", g.Human, "caocao", "奸雄", (Action)(() => { })); Check(g.Human.Zone("田").Count == 0 && g.DiscardPile.Contains(pile), "Tuntian onremove discards actual field cards when changing borrowed skill");
        s = IncarnationRig6("guojia", "天妒"); s.Players[0] = s.Players[0] with { IncarnationSkill = "遗计" }; g = Restore(s); int souls = g.Human.Incarnations.Count; done = false; Call(g, "ElementalDamage", g.Players[1], g.Human, null, 2, DamageNature.Normal, (Action)(() => done = true)); Decision(g, "yes"); Decision(g, "yes"); Decision(g, "no"); Decision(g, "no"); Settle3(g, () => done); Check(g.Human.Incarnations.Count == souls + 2, "Xinsheng acquires one additional eligible soul per damage point, alongside borrowed passive skill");
        s = IncarnationRig6("caocao", "奸雄"); s.Players[0] = s.Players[0] with { IncarnationSkill = "护驾" }; try { Restore(s); Check(false, "Reject borrowed lord skill"); } catch (JsonException) { Check(true, "Save validator rejects forbidden lord skill borrow"); }
    }
    private static void VerifyBorrowedTriggers6()
    {
        foreach ((string form, string selected, bool expected) in new[] { ("guojia","天妒",true), ("guojia","遗计",false) })
        {
            SavedGame s = IncarnationRig6(form, selected); Card top = PutTop(s, c => c.IsRed); GameEngine g = Restore(s); bool done = false; Call(g, "Judge", g.Human, "借用天妒测试", (Action<Card?>)(_ => done = true));
            if (expected) Decision(g, "yes"); Settle3(g, () => done); Check(g.Human.Hand.Contains(top) == expected, "Actual judgment trigger only grants selected Tiandu, not companion skill");
        }
        foreach ((string form, string selected, int expected) in new[] { ("zhouyu","英姿",3), ("zhouyu","反间",2), ("re_zhouyu","英姿",3), ("lusu","好施",4), ("lusu","缔盟",2) })
        {
            SavedGame s = IncarnationRig6(form, selected); GameEngine g = Restore(s); Call(g, "ResolveDrawPhase", g.Human, false); if (g.PendingDecision != null) Decision(g, "yes"); Settle3(g); Check(g.Human.Hand.Count == expected, "Only borrowed draw modifier applies: " + form + selected);
        }
        SavedGame state = IncarnationRig6("wolong", "八阵"); Card red = Take3(state, 0, c => c.IsRed); GameEngine game = Restore(state); Check(!game.CanActivateSkill(game.Human), "Borrowing Bazhen never grants Huoji");
        state = IncarnationRig6("wolong", "火计"); red = Take3(state, 0, c => c.IsRed); Card other = Move(state, CardKind.Dodge, 1); game = Restore(state); Check(game.UseSkill(null, 1), "Borrowed Huoji is a real active skill"); Decision(game, red.Id.ToString()); Settle3(game); Check(game.CardEvents.Any(e => e.Kind == CardKind.FireAttack), "Borrowed Huoji emits/resolves actual virtual FireAttack");
        state = IncarnationRig6("re_caocao", "奸雄"); Card damageCard = Move(state, CardKind.Slash, 1); game = Restore(state);
        Call(game, "ResolveCard", game.Players[1], damageCard, game.Human, false); Check(game.Pending != null && game.Respond(null), "Borrowed Jianxiong damage starts normal rescue/response flow"); Decision(game, "no"); Decision(game, "yes"); Settle3(game, () => game.CardEvents.Any(e => e.Kind == CardKind.Slash && e.Stage == CardUseStage.Finished));
        Check(game.Human.Hand.Contains(damageCard) && game.Human.Hand.Count == 2, "Xinsheng coexists with borrowed refreshed Jianxiong: real damage card plus one draw");
        state = IncarnationRig6("re_xiahoudun", "刚烈"); PutTop(state, card => card.IsRed); game = Restore(state); bool damageDone = false; int sourceHp = game.Players[1].Health;
        Call(game, "ElementalDamage", game.Players[1], game.Human, null, 1, DamageNature.Normal, (Action)(() => damageDone = true)); Decision(game, "no"); Decision(game, "yes"); Settle3(game, () => damageDone); Check(game.Players[1].Health == sourceHp - 1, "Xinsheng and borrowed refreshed Ganglie both resolve, neither shadows the other");
        state = Rig3("liushan"); state.Players[0] = state.Players[0] with { Awakened = true, MaxHealth = 5, Health = 5 }; game = Restore(state); Check(game.CanActivateSkill(game.Human), "Awakened Liu Shan can actually activate acquired lord Jijiang");
        state = Rig3("sunce"); state.Players[0] = state.Players[0] with { Awakened = true, MaxHealth = 4, Health = 2, TemporarySkillBans = new() { SkillCategory.Ordinary } }; game = Restore(state); Check(game.Human.HasAbility("英姿") && !game.Human.HasAbility("英魂") && game.Human.HandLimit == 4, "Hunzi-acquired RE-Yingzi remains locked under ordinary seals, unlike Yinghun");
        state = IncarnationRig6("re_guanyu", "武圣"); Card diamond = Take3(state, 0, c => c.Kind == CardKind.Slash && c.Suit == "♦"); game = Restore(state); Check(game.ValidatePlay(game.Human, diamond, game.Players[2]) == null && !game.Human.HasAbility("义绝"), "Borrowed refreshed Wusheng preserves variant modifier but not Yijue");
    }
}
