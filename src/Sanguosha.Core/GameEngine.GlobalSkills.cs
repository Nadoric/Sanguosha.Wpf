namespace Sanguosha.Core;
public sealed partial class GameEngine
{
    private bool selectingGlobal;
    public bool HasGlobalActive(Player actor) => GlobalActive(actor).Count > 0;
    private List<(string Key, Player Lord)> GlobalActive(Player actor)
    {
        List<(string, Player)> skills = new();
        foreach (Player lord in Players.Where(player => player.IsAlive && player != actor && Lord(player)))
        {
            if (((lord.RuleId == "zhangjiao" && lord.HasAbility("黄天"))) && actor.Kingdom == "群" && actor.Mark("huangtianUsed") == 0 && actor.Hand.Any(card => card.Kind is CardKind.Dodge or CardKind.Lightning)) skills.Add(("huangtian", lord));
            if (((lord.RuleId == "sunce" && lord.HasAbility("制霸"))) && actor.Kingdom == "吴" && actor.Mark("zhibaUsed") == 0 && actor.Hand.Count > 0 && lord.Hand.Count > 0) skills.Add(("zhiba", lord));
        }
        return skills;
    }
    private bool GlobalSkillMenu(Player actor, Card? card, Player? target, Player? second)
    {
        List<(string Key, Player Lord)> global = GlobalActive(actor); if (!actor.IsHuman || selectingGlobal || global.Count == 0) return false;
        List<SkillDecisionOption> options = global.Select(skill => new SkillDecisionOption(skill.Key, skill.Key == "huangtian" ? "黄天：给主公闪/闪电" : "制霸：与孙策主公拼点")).ToList();
        options.Add(new("own", "发动自己的主动技能 / 取消"));
        Phase = GamePhase.Resolving; Decide(actor, "可用公共主公技能", options, "own", key =>
        {
            if (key == "own") { selectingGlobal = true; bool used = ExecuteActiveSkill(actor, card, target, second); selectingGlobal = false; if (!used) CompleteAction(actor); }
            else ExecuteGlobal(actor, global.First(skill => skill.Key == key).Key, global.First(skill => skill.Key == key).Lord, () => CompleteAction(actor));
        }); return true;
    }
    private void ExecuteGlobal(Player actor, string skill, Player lord, Action done)
    {
        if (skill == "huangtian")
        {
            ChooseOwnCards(actor, "黄天：交给主公一张闪或闪电", 1, 1, false, cards =>
            { actor.SetMark("huangtianUsed", 1); actor.Hand.Remove(cards[0]); Gain(lord, cards[0]); OnHandLost(actor); EmitSkill(lord, "黄天"); done(); }, card => card.Kind is CardKind.Dodge or CardKind.Lightning); return;
        }
        actor.SetMark("zhibaUsed", 1);
        void Compare()
        {
            EmitSkill(lord, "制霸"); CompareHands(actor, lord, won =>
            { if (won) done(); else OptionalSkill(lord, "制霸：是否获得两张仍在弃牌堆的拼点牌？", true, use => { if (use) foreach (Card card in comparisonCards.ToList()) if (DiscardPile.Remove(card)) Gain(lord, card); done(); }); });
        }
        if (lord.Awakened) OptionalSkill(lord, "制霸：觉醒后是否拒绝此次拼点？", Hostility(lord, actor) > 0, refuse => { if (refuse) done(); else Compare(); }); else Compare();
    }
    private void JijiangDraw(Player helper, Action done)
    {
        Player? lord = Players.FirstOrDefault(player => player.IsAlive && Lord(player) && player != helper && (((player.SkillIdentity == "re_liubei" && player.HasAbility("激将"))) && !player.SkillsDisabled || ((player.RuleId == "liushan" && player.HasAbility("激将"))) && player.Awakened) && player.Mark("jijiangDraw") == 0);
        if (lord == null || helper == Current || helper.Kingdom != "蜀") { done(); return; }
        OptionalSkill(helper, $"界激将：是否令主公{lord.Hero.Name}摸一张？每回合一次", Allied(helper, lord), use => { if (use) { lord.SetMark("jijiangDraw", 1); Draw(lord, 1); EmitSkill(lord, "激将"); } done(); });
    }
    private void OnCardDeclared(CardUseFrame frame, Action done)
    {
        void Jizhi()
        {
            if (((frame.Actor.RuleId == "huangyueying" && frame.Actor.HasAbility("集智"))) && Trick(frame.Kind) && (!Delayed(frame.Kind) || ((frame.Actor.SkillIdentity == "re_huangyueying" && frame.Actor.HasAbility("集智")))))
                OptionalSkill(frame.Actor, "集智：是否摸一张牌？", true, use => { if (use) JizhiDraw(frame.Actor, done); else done(); });
            else done();
        }
        void Jiang()
        {
            bool eligible = frame.Kind == CardKind.Duel || frame.Kind == CardKind.Slash && frame.Materials.Count > 0 && frame.Materials.All(card => RedFor(frame.Actor, card));
            List<Player> owners = eligible ? new[] { frame.Actor }.Concat(frame.Kind == CardKind.Duel ? frame.Targets : Enumerable.Empty<Player>()).Where(player => ((player.RuleId == "sunce" && player.HasAbility("激昂")))).Distinct().ToList() : new();
            void Next(int index) { if (index >= owners.Count) Jizhi(); else OptionalSkill(owners[index], "激昂：是否摸一张牌？", true, use => { if (use) { Draw(owners[index], 1); EmitSkill(owners[index], "激昂"); } Next(index + 1); }); }
            Next(0);
        }
        List<Player> owners = Trick(frame.Kind) && frame.Targets.Count >= 2 ? Players.Where(player => player.IsAlive && ((player.SkillIdentity == "re_ganning" && player.HasAbility("奋威"))) && !player.SkillsDisabled && !player.LimitedUsed).ToList() : new();
        void Fenwei(int index)
        {
            if (index >= owners.Count) { if (((frame.Actor.SkillIdentity == "re_zhaoyun" && frame.Actor.HasAbility("龙胆"))) && !frame.Actor.SkillsDisabled && frame.Actor != Current && frame.Materials.Count > 0) Yajiao(frame.Actor, new Card(0, frame.Kind, "", 0).Category, Jiang); else Jiang(); return; }
            Player owner = owners[index]; List<Player> chosen = new();
            void Pick()
            {
                ChoosePlayer(owner, "奋威（限定）：选择要免除此锦囊效果的目标，或完成", frame.Targets.Where(player => !chosen.Contains(player) && !frame.Excluded.Contains(player.Seat)), true,
                    player => frame.Kind == CardKind.Feast ? Hostility(owner, player) : Allied(owner, player) ? 50 : -50, target =>
                {
                    if (target != null) { chosen.Add(target); Pick(); return; }
                    if (chosen.Count > 0) { owner.LimitedUsed = true; foreach (Player player in chosen) frame.Excluded.Add(player.Seat); EmitSkill(owner, "奋威"); } Fenwei(index + 1);
                });
            }
            Pick();
        }
        if (frame.Kind == CardKind.Slash) JijiangDraw(frame.Actor, () => Fenwei(0)); else Fenwei(0);
    }
    private void Yajiao(Player owner, string category, Action done)
    {
        if (!owner.IsAlive) { done(); return; }
        OptionalSkill(owner, "涯角：回合外使用/打出手牌，是否展示牌堆顶牌？", true, use =>
        {
            if (!use) { done(); return; } Card? top = TakeTop(); if (top == null) { done(); return; }
            OrderingCards.Add(top); ShowCard(top); EmitSkill(owner, "涯角");
            void Finish(bool gained) { OrderingCards.Remove(top); if (!gained) deck.Add(top); done(); }
            if (top.Category == category)
                ChoosePlayer(owner, "涯角：可将此牌交给任意角色，或放回顶", Players, true, player => Allied(owner, player) ? 10 + 5 - player.Hand.Count : -10, target => { if (target == null) Finish(false); else { Gain(target, top); Finish(true); } });
            else ChoosePlayer(owner, "涯角：可弃攻击范围包含你的角色区域内一牌，顶牌之后放回", Players.Where(player => player != owner && Distance(player, owner) <= player.AttackRange && AvailableTargetCards(owner, player, false) > 0), true, player => Hostility(owner, player), target =>
                { if (target == null) Finish(false); else ChooseOtherCardIncludingJudgment(owner, target, false, () => Finish(false)); });
        });
    }
    private void BeforeSlashTarget(Player actor, Player target, Card card, Action<Player> resolved, Action canceled)
    {
        if (!target.IsAlive) { canceled(); return; }
        void Xiangle(Player final)
        {
            if (!((final.RuleId == "liushan" && final.HasAbility("享乐")))) { resolved(final); return; }
            List<Card> cards = actor.Hand.Where(item => item.IsBasic).ToList(); List<SkillDecisionOption> options = cards.Select(item => new SkillDecisionOption(item.Id.ToString(), "享乐：弃基本牌 · " + CardLabel(item))).ToList(); options.Add(new("no", "不弃，此杀无效"));
            Decide(actor, "享乐：额外弃置一张基本牌，否则杀无效", options, cards.Count > 0 && Hostility(actor, final) > 0 ? cards.MinBy(KeepValue)!.Id.ToString() : "no", key =>
            { EmitSkill(final, "享乐"); if (key == "no") canceled(); else { PayCard(actor, cards.First(item => item.Id.ToString() == key)); AfterLosses(() => resolved(final)); } });
        }
        void Tongji()
        {
            Player? yuanshu = Players.FirstOrDefault(player => player.IsAlive && ((player.RuleId == "yuanshu" && player.HasAbility("同疾"))) && player != actor && player != target && Distance(target, player) <= target.AttackRange);
            if (yuanshu == null || !He(target).Any() || cardUses.LastOrDefault()?.Targets.Contains(yuanshu) == true) { Xiangle(target); return; }
            OptionalSkill(target, $"同疾：弃一牌，将杀转移给攻击范围内的{yuanshu.Hero.Name}？", Hostility(target, yuanshu) > 0, use =>
            { if (!use) Xiangle(target); else ChooseOwnCards(target, "同疾：弃一张手牌/装备", 1, 1, true, cards => { PayCard(target, cards[0]); if (cardUses.LastOrDefault() is { } frame) { frame.Targets.Remove(target); frame.Targets.Add(yuanshu); } EmitSkill(yuanshu, "同疾"); AfterLosses(() => Xiangle(yuanshu)); }); });
        }
        if (((target.RuleId == "sunce" && target.HasAbility("激昂"))) && RedFor(actor, card)) OptionalSkill(target, "激昂：成为红杀目标，是否摸一张？", true, use => { if (use) { Draw(target, 1); EmitSkill(target, "激昂"); } Tongji(); });
        else Tongji();
    }
    private void Songwei(Player judged, Card card, Action done)
    {
        Player? lord = Players.FirstOrDefault(player => player.IsAlive && player != judged && ((player.RuleId == "caopi" && player.HasAbility("颂威"))) && Lord(player));
        if (lord == null || judged.Kingdom != "魏" || RedFor(judged, card)) { done(); return; }
        OptionalSkill(judged, $"颂威：黑色判定，是否令主公{lord.Hero.Name}摸一张？", Allied(judged, lord), use => { if (use) { Draw(lord, 1); EmitSkill(lord, "颂威"); } done(); });
    }
}

public sealed class ObservedDiscardPile : System.Collections.ObjectModel.Collection<Card>
{
    private readonly Action<Card> discarded;
    public ObservedDiscardPile(Action<Card> observer) => discarded = observer;
    protected override void InsertItem(int index, Card card) { base.InsertItem(index, card); discarded(card); }
    protected override void SetItem(int index, Card card) { base.SetItem(index, card); discarded(card); }
    public void AddRange(IEnumerable<Card> cards) { foreach (Card card in cards.ToList()) Add(card); }
}
