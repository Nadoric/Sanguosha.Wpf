namespace Sanguosha.Core;

public sealed record HandBatchEvent(int Seat, bool Gained, IReadOnlyList<int> CardIds);
public sealed partial class GameEngine
{
    public List<HandBatchEvent> HandEvents { get; } = new();
    private int? normalDrawSeat;
    private bool finishingGlobalTurn;
    private bool skippedIncarnationTurn;
    private readonly Dictionary<int, List<int>> handLossBatches = new();
    private void PublishHand(Player player, bool gained, IEnumerable<Card> cards)
    {
        int[] ids = cards.Select(card => card.Id).ToArray(); if (ids.Length == 0) return;
        HandEvents.Add(new(player.Seat, gained, ids)); if (HandEvents.Count > 4096) HandEvents.RemoveRange(0, 1024);
    }
    private void Gain(Player player, IEnumerable<Card> cards)
    {
        List<Card> batch = cards.ToList(); player.Hand.AddRange(batch); PublishHand(player, true, batch);
        if (batch.Count == 0 || Phase == GamePhase.NotStarted || normalDrawSeat == player.Seat || !player.HasAbility("清俭") || player.Mark("qingjianUsed") > 0) return;
        reactions.Enqueue(done => Qingjian(player, done));
    }
    private void Gain(Player player, Card card) => Gain(player, new[] { card });
    private void GiveCards(Player source, Player target, IEnumerable<Card> cards)
    {
        List<Card> batch = cards.ToList(); List<Card> lostHand = batch.Where(source.Hand.Contains).ToList(); int equipment = 0;
        foreach (Card card in batch) { if (source.Equipment.Remove(card)) equipment++; else source.Hand.Remove(card); }
        if (lostHand.Count > 0) { PublishHand(source, false, lostHand); OnHandLost(source, lostHand.Count); }
        for (int i = 0; i < equipment; i++) OnEquipmentLost(source);
        Gain(target, batch);
    }
    private void PayCards(Player owner, IEnumerable<Card> cards)
    {
        List<Card> batch = cards.ToList(); bool outer = !handLossBatches.ContainsKey(owner.Seat);
        if (outer) handLossBatches[owner.Seat] = new();
        foreach (Card card in batch) PayCard(owner, card);
        if (outer) { List<int> lost = handLossBatches[owner.Seat]; handLossBatches.Remove(owner.Seat); if (lost.Count > 0) OnHandLost(owner, lost.Count); }
    }
    private void Qingjian(Player owner, Action done)
    {
        if (!owner.IsAlive || !owner.HasAbility("清俭") || owner.Mark("qingjianUsed") > 0 || !He(owner).Any()) { done(); return; }
        ChoosePlayer(owner, "清俭（每回合一次）：可展示并交给另一人任意手牌/装备", Players.Where(player => player != owner), true,
            target => Allied(owner, target) && Allied(owner, Current) ? 8 - target.Hand.Count : -10, target =>
        {
            if (target == null) { done(); return; }
            ChooseOwnCards(owner, "清俭：选择赠牌（不同类别增加当前回合手牌上限）", 1, He(owner).Count(), true, cards =>
            {
                owner.SetMark("qingjianUsed", 1); foreach (Card card in cards) ShowCard(card);
                Current.SetMark("qingjianBonus", Current.Mark("qingjianBonus") + cards.Select(card => card.Category).Distinct().Count());
                GiveCards(owner, target, cards); EmitSkill(owner, "清俭"); AfterLosses(done);
            });
        });
    }
    private void SealNonLocked(Player target, Action done)
    {
        target.TemporarySkillBans.UnionWith(new[] { SkillCategory.Ordinary, SkillCategory.Lord, SkillCategory.Limited });
        if (target.Health <= 0 && !IsBuquAlive(target)) StartRescue(null, target, done); else done();
    }
    private bool ExecuteYijue(Player actor)
    {
        if (!actor.HasAbility("义绝") || actor.Mark("yijueUsed") > 0 || !He(actor).Any() || !Players.Any(player => player.IsAlive && player != actor && player.Hand.Count > 0)) return false;
        Phase = GamePhase.Resolving;
        ChoosePlayer(actor, "义绝：选择有手牌的其他角色，或取消", Players.Where(player => player != actor && player.Hand.Count > 0), true,
            target => Hostility(actor, target), target =>
        {
            if (target == null) { CompleteAction(actor); return; }
            ChooseOwnCards(actor, "义绝：弃置一张手牌/装备", 1, 1, true, costs =>
            {
                actor.SetMark("yijueUsed", 1); PayCards(actor, costs); EmitSkill(actor, "义绝");
                AfterLosses(() =>
                {
                    if (!actor.IsAlive || !target.IsAlive || target.Hand.Count == 0) { CompleteAction(actor); return; }
                    Card best = target.Hand.Where(card => RedFor(target, card)).MinBy(KeepValue) ?? target.Hand.MinBy(KeepValue)!;
                    Decide(target, "义绝：展示一张手牌（黑色被封；红色交给关羽）", target.Hand.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card))).ToList(), best.Id.ToString(), key =>
                    {
                        Card revealed = target.Hand.First(card => card.Id.ToString() == key); ShowCard(revealed); bool red = RedFor(target, revealed);
                        AddLog($"义绝：{target.Hero.Name}展示{CardLabel(revealed)}。");
                        if (!red)
                        {
                            target.HandUseBanned = true; target.SetMark($"yijueFrom{actor.Seat}", target.Mark($"yijueFrom{actor.Seat}") + 1);
                            SealNonLocked(target, () => CompleteAction(actor));
                        }
                        else
                        {
                            GiveCards(target, actor, new[] { revealed });
                            AfterLosses(() => OptionalSkill(actor, $"义绝：是否令{target.Hero.Name}回复1点体力？", Allied(actor, target), use =>
                            { if (use && target.Health < target.MaxHealth) HealWithChoices(target, () => CompleteAction(actor)); else CompleteAction(actor); }));
                        }
                    });
                });
            });
        }); return true;
    }
    private void RefreshedTieji(Player actor, Player target, Card card, Action done)
    {
        OptionalSkill(actor, "界铁骑：判定并封禁目标非锁定技，要求交出同花色牌？", true, use =>
        {
            if (!use) { ResolveSlashDefenseCore(actor, target, card, done); return; }
            EmitSkill(actor, "铁骑"); Judge(actor, "界铁骑", judgment => SealNonLocked(target, () =>
            {
                if (!actor.IsAlive || !target.IsAlive) { done(); return; }
                List<Card> costs = judgment == null ? new() : He(target).Where(item => SuitFor(target, item) == SuitFor(actor, judgment)).ToList();
                var options = costs.Select(item => new SkillDecisionOption(item.Id.ToString(), "交给来源 · " + CardLabel(item))).ToList(); options.Add(new("no", "不交牌：不能闪，此杀伤害+1"));
                string ai = costs.Count > 0 && (target.Health <= 2 || target.Hand.Any(item => CanTransform(target, item, ResponseKind.Dodge))) ? costs.MinBy(KeepValue)!.Id.ToString() : "no";
                Decide(target, "界铁骑：交给来源一张与判定同花色的手牌/装备", options, ai, key =>
                {
                    if (key == "no")
                    {
                        CardUseFrame? frame = cardUses.LastOrDefault(use => use.Actor == actor && use.Kind == CardKind.Slash);
                        if (frame != null) { frame.DamageBonus++; Damage(actor, target, card, done, CardKind.Slash); }
                        else { int previous = actor.Mark("tiejiDamage"); actor.SetMark("tiejiDamage", previous + 1); Damage(actor, target, card, () => { actor.SetMark("tiejiDamage", previous); done(); }, CardKind.Slash); }
                    }
                    else { GiveCards(target, actor, new[] { costs.First(item => item.Id.ToString() == key) }); AfterLosses(() => ResolveSlashDefenseCore(actor, target, card, done)); }
                });
            }));
        });
    }
    private void RefreshedGanglie(Player? source, Player owner, int amount, Action done)
    {
        void Next(int index)
        {
            if (index >= amount || !owner.IsAlive || !owner.HasAbility("刚烈") || Phase == GamePhase.GameOver) { done(); return; }
            OptionalSkill(owner, $"界刚烈：第{index + 1}/{amount}点伤害，是否判定？红伤黑弃", source?.IsAlive == true && !Allied(owner, source), use =>
            {
                if (!use) { Next(index + 1); return; }
                EmitSkill(owner, "刚烈"); Judge(owner, "界刚烈", judgment =>
                {
                    if (judgment == null || source?.IsAlive != true) { Next(index + 1); return; }
                    if (RedFor(owner, judgment)) Damage(owner, source, null, () => Next(index + 1));
                    else if (AvailableTargetCards(owner, source, false, false) > 0) ChooseOtherCard(owner, source, false, () => Next(index + 1));
                    else Next(index + 1);
                });
            });
        }
        Next(0);
    }
    private void Qianxun(Player owner, Action done)
    {
        if (!owner.IsAlive || owner.SkillIdentity != "re_luxun" || !owner.HasAbility("谦逊") || owner.Hand.Count == 0) { done(); return; }
        OptionalSkill(owner, "界谦逊：将全部手牌暂置武将牌，本回合结束一并取回？", owner.Hand.Count > 1, use =>
        {
            if (!use) { done(); return; }
            List<Card> hand = owner.Hand.ToList(); owner.Hand.Clear(); owner.Zone("谦逊").AddRange(hand); PublishHand(owner, false, hand); OnHandLost(owner, hand.Count); EmitSkill(owner, "谦逊"); AfterLosses(done);
        });
    }
    private void RefreshedLianying(Player owner, int lost)
    {
        reactions.Enqueue(done =>
        {
            if (!owner.IsAlive || !owner.HasAbility("连营") || owner.SkillIdentity != "re_luxun" || owner.Hand.Count > 0) { done(); return; }
            List<Player> targets = new();
            void Pick()
            {
                if (targets.Count >= Math.Min(lost, Players.Count(player => player.IsAlive))) { Finish(); return; }
                ChoosePlayer(owner, $"界连营：至多{lost}名不同角色各摸一（已选{targets.Count}）", Players.Where(player => !targets.Contains(player)), true,
                    target => Allied(owner, target) ? 10 - target.Hand.Count : -10, target => { if (target == null) Finish(); else { targets.Add(target); Pick(); } });
            }
            void Finish() { if (targets.Count > 0) { foreach (Player target in targets) Draw(target, 1); EmitSkill(owner, "连营"); } done(); }
            Pick();
        });
    }
    private void FinishGlobalTurn(Action next)
    {
        if (finishingGlobalTurn) { next(); return; }
        finishingGlobalTurn = true;
        foreach (Player player in Players.Where(player => player.IsAlive && player.NamedZones.TryGetValue("谦逊", out var cards) && cards.Count > 0))
        { List<Card> hand = player.Zone("谦逊").ToList(); player.Zone("谦逊").Clear(); Gain(player, hand); AddLog($"{player.Hero.Name}取回谦逊暂存的{hand.Count}张手牌。"); }
        void Expire()
        {
            foreach (Player player in Players)
            {
                player.TemporarySkillBans.Clear(); player.HandUseBanned = false; player.SetMark("qingjianUsed", 0); player.SetMark("qingjianBonus", 0);
                foreach (string key in player.Marks.Keys.Where(key => key.StartsWith("yijueFrom", StringComparison.Ordinal)).ToList()) player.SetMark(key, 0);
            }
            finishingGlobalTurn = false; skippedIncarnationTurn = false; next();
        }
        AfterLosses(() => { if (skippedIncarnationTurn) Expire(); else ChooseIncarnation(Current, true, Expire); });
    }
}
