namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private readonly HashSet<int> pendingTuntian = new();
    private readonly HashSet<string> discardedSuits = new();
    private readonly List<Card> comparisonCards = new();
    private bool Lord(Player player) => player.Role == Role.Lord && Mode != GameMode.Team2V2;
    private bool IsBatchThreeActive(Player player) => player.HasActiveAbility && (player.RuleId is "xunyu" or "yuanshao" or "wolong" or "dongzhuo" or "jiaxu" or "dengai" or "yiji" || player.RuleId == "liushan" && player.Awakened ||
        player.SkillIdentity is "re_huatuo" or "re_daqiao" or "re_liubei" or "re_sunquan" || player.SkillIdentity == "re_lvmeng" && player.Awakened);

    private void BatchThreePreparation(Player actor, Action done)
    {
        foreach (Player owner in Players.Where(player => player.IsAlive && ((player.RuleId == "yuanshu" && player.HasAbility("妄尊"))) && actor.Health > player.Health))
        { Draw(owner, Lord(actor) ? 2 : 1); if (Lord(actor)) actor.SetMark("wangzunPenalty", actor.Mark("wangzunPenalty") + 1); EmitSkill(owner, "妄尊"); }
        void Next()
        {
            if (((actor.SkillIdentity == "re_zhangfei" && actor.HasAbility("替身"))) && !actor.SkillsDisabled && !actor.LimitedUsed && actor.Health < actor.MaxHealth)
            {
                OptionalSkill(actor, "替身（限定）：回复至体力上限，并摸等同实际回复量的牌？", actor.Health <= 2, use =>
                { if (use) { int count = actor.MaxHealth - actor.Health; actor.LimitedUsed = true; EmitSkill(actor, "替身"); RecoverMany(actor, count, () => { Draw(actor, count); done(); }); } else done(); }); return;
            }
            if (((actor.RuleId == "ganfuren" && actor.HasAbility("神智"))) && actor.Hand.Count > 0)
            {
                OptionalSkill(actor, "神智：弃全部手牌，若张数至少等于体力则回复1？", actor.Health <= 2 && actor.Hand.Count >= actor.Health && actor.Hand.Count <= 3 && actor.Hand.All(card => KeepValue(card) < 7), use =>
                { if (!use) { done(); return; } int count = actor.Hand.Count; PayCards(actor, actor.Hand.ToList()); EmitSkill(actor, "神智"); if (count >= actor.Health) HealWithChoices(actor, done); else done(); }); return;
            }
            done();
        }
        if (!actor.Awakened && ((actor.RuleId == "dengai" && actor.HasAbility("凿险"))) && actor.Zone("田").Count >= 3)
        { actor.Awakened = true; actor.MaxHealth--; actor.Health = Math.Min(actor.Health, actor.MaxHealth); EmitSkill(actor, "凿险"); Next(); return; }
        if (!actor.Awakened && ((actor.RuleId == "liushan" && actor.HasAbility("若愚"))) && Lord(actor) && Players.Where(player => player.IsAlive).All(player => player.Health >= actor.Health))
        { actor.Awakened = true; actor.MaxHealth++; EmitSkill(actor, "若愚"); HealWithChoices(actor, Next); return; }
        if (!actor.Awakened && ((actor.RuleId == "sunce" && actor.HasAbility("魂姿"))) && actor.Health <= 1)
        { actor.Awakened = true; actor.MaxHealth--; actor.Health = Math.Min(actor.Health, actor.MaxHealth); EmitSkill(actor, "魂姿"); Yinghun(actor, Next); return; }
        if (((actor.RuleId == "sunce" && actor.HasAbility("英魂"))) && actor.Awakened) { Yinghun(actor, Next); return; }
        Qinxue(actor, Next);
    }
    private void RecoverMany(Player player, int count, Action done)
    { if (count <= 0 || player.Health >= player.MaxHealth) done(); else HealWithChoices(player, () => RecoverMany(player, count - 1, done)); }
    private void Yinghun(Player actor, Action done)
    {
        int count = actor.MaxHealth - actor.Health;
        if (count <= 0) { done(); return; }
        ChoosePlayer(actor, "英魂：可选另一人摸X弃一或摸一弃X", Players.Where(player => player != actor), true, target => Allied(actor, target) ? count : count - 1, target =>
        { if (target == null) { done(); return; } Decide(actor, "英魂：选择效果", new[] { new SkillDecisionOption("draw", $"摸{count}弃一"), new SkillDecisionOption("discard", $"摸一弃{count}") }, Allied(actor, target) ? "draw" : "discard", key =>
            { Draw(target, key == "draw" ? count : 1); EmitSkill(actor, "英魂"); DiscardOwnCards(target, "英魂：弃牌", key == "draw" ? 1 : count, true, done); }); });
    }
    private void Qinxue(Player actor, Action done)
    {
        if (!((actor.SkillIdentity == "re_lvmeng" && actor.HasAbility("勤学"))) || actor.SkillsDisabled || actor.Awakened || actor.Hand.Count < actor.Health + 2) { done(); return; }
        actor.Awakened = true; actor.MaxHealth--; actor.Health = Math.Min(actor.Health, actor.MaxHealth); EmitSkill(actor, "勤学");
        Decide(actor, "勤学觉醒：回复1点或摸2张，获得攻心", new[] { new SkillDecisionOption("heal", "回复一"), new SkillDecisionOption("draw", "摸二") }, actor.Health < actor.MaxHealth ? "heal" : "draw", key =>
        { if (key == "heal") HealWithChoices(actor, done); else { Draw(actor, 2); done(); } });
    }
    private bool BatchThreeDraw(Player actor, Action done)
    {
        if (((actor.RuleId == "sunce" && actor.HasAbility("英姿"))) && actor.Awakened || ((actor.RuleId == "re_zhouyu" && actor.HasAbility("英姿")))) { Draw(actor, 3); EmitSkill(actor, "英姿"); done(); return true; }
        if (!((actor.RuleId == "lidian" && actor.HasAbility("恂恂")))) return false;
        OptionalSkill(actor, "恂恂：改为观看顶四张，选两张收入手牌，其余排序置底？", true, use =>
        {
            if (!use) { Draw(actor, 2); done(); return; }
            List<Card> cards = new(); for (int i = 0; i < 4; i++) { Card? card = TakeTop(); if (card != null) { cards.Add(card); OrderingCards.Add(card); } }
            List<Card> chosen = new();
            void Pick()
            {
                if (chosen.Count == Math.Min(2, cards.Count)) { foreach (Card card in chosen) { OrderingCards.Remove(card); Gain(actor, card); } Bottom(cards.Except(chosen).ToList()); return; }
                List<Card> remaining = cards.Except(chosen).ToList(); Decide(actor, "恂恂：选择收入手牌", remaining.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card))).ToList(), remaining.MaxBy(KeepValue)!.Id.ToString(), key => { chosen.Add(remaining.First(card => card.Id.ToString() == key)); Pick(); });
            }
            void Bottom(List<Card> remaining)
            {
                if (remaining.Count == 0) { EmitSkill(actor, "恂恂"); done(); return; }
                Decide(actor, "恂恂：逐张置底（最后选的位于最底）", remaining.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card))).ToList(), remaining[0].Id.ToString(), key => { Card card = remaining.First(card => card.Id.ToString() == key); OrderingCards.Remove(card); deck.Insert(0, card); remaining.Remove(card); Bottom(remaining); });
            }
            Pick();
        }); return true;
    }
    private void BeforePlayBatchThree(Player actor, Action done)
    {
        if (!((actor.RuleId == "liushan" && actor.HasAbility("放权")))) { done(); return; }
        OptionalSkill(actor, "放权：跳过出牌阶段，回合结束可弃手牌让另一人额外回合？", Players.Any(player => player.IsAlive && player != actor && Allied(actor, player)) && actor.Health >= 2, use =>
        { if (!use) done(); else { actor.SetMark("fangquan", 1); EmitSkill(actor, "放权"); BeginDiscard(actor); } });
    }
    private void BatchThreeEnd(Player actor, Action done)
    {
        void Finish()
        {
            actor.SetMark("wine", 0); actor.SetMark("wineSlash", 0); actor.SetMark("paoxiaoBoost", 0);
            if (((actor.SkillIdentity == "re_zhugeliang" && actor.HasAbility("空城"))) && !actor.SkillsDisabled && actor.Mark("guanxingBottom") > 0)
            { OptionalSkill(actor, "界观星：准备阶段全部置底，结束阶段可再次观星", true, use => { if (use) Guanxing(actor, done); else done(); }); return; }
            done();
        }
        void Botu()
        {
            if (!((actor.SkillIdentity == "re_lvmeng" && actor.HasAbility("博图"))) || actor.SkillsDisabled || discardedSuits.Count < 4) { Finish(); return; }
            if (actor.Mark("botuRound") != Round) { actor.SetMark("botuRound", Round); actor.SetMark("botuCount", 0); }
            if (actor.Mark("botuCount") >= Math.Min(3, Players.Count(player => player.IsAlive))) { Finish(); return; }
            OptionalSkill(actor, "博图：本回合弃牌出现四花色，是否获得额外回合？", true, use => { if (use) { actor.SetMark("botuCount", actor.Mark("botuCount") + 1); extraTurns.Enqueue(actor.Seat); EmitSkill(actor, "博图"); } Finish(); });
        }
        void Fangquan()
        {
            if (!((actor.RuleId == "liushan" && actor.HasAbility("放权"))) || actor.Mark("fangquan") == 0 || actor.Hand.Count == 0) { Qinxue(actor, Botu); return; }
            ChoosePlayer(actor, "放权：弃一手牌令其他角色额外回合，或不发动", Players.Where(player => player != actor), true, player => -Hostility(actor, player), target =>
            {
                if (target == null) { Qinxue(actor, Botu); return; }
                ChooseOwnCards(actor, "放权：弃一手牌", 1, 1, false, cards => { PayCard(actor, cards[0]); extraTurns.Enqueue(target.Seat); EmitSkill(actor, "放权"); Qinxue(actor, Botu); });
            });
        }
        void Collapse()
        {
            if (!((actor.RuleId == "dongzhuo" && actor.HasAbility("崩坏"))) || !Players.Any(player => player.IsAlive && player.Health < actor.Health)) { Fangquan(); return; }
            Decide(actor, "崩坏：失去一点体力或一点体力上限", new[] { new SkillDecisionOption("hp", "失去体力"), new SkillDecisionOption("max", "减少体力上限") }, actor.Health < actor.MaxHealth ? "max" : "hp", key =>
            {
                EmitSkill(actor, "崩坏"); if (key == "hp") LoseHealth(actor, 1, Fangquan);
                else { actor.MaxHealth--; actor.Health = Math.Min(actor.Health, actor.MaxHealth); if (actor.MaxHealth <= 0) EliminateWithTriggers(null, actor, Fangquan); else Fangquan(); }
            });
        }
        List<Player> owners = Players.Where(player => player.IsAlive && player != actor && ((player.RuleId == "yuejin" && player.HasAbility("骁果"))) && player.Hand.Any(card => card.IsBasic)).ToList();
        void Xiaoguo(int index)
        {
            if (index >= owners.Count || !actor.IsAlive || Phase == GamePhase.GameOver) { Collapse(); return; }
            Player owner = owners[index]; OptionalSkill(owner, $"骁果：弃基本手牌，令{actor.Hero.Name}弃装备或受伤？", Hostility(owner, actor) > 0, use =>
            {
                if (!use) { Xiaoguo(index + 1); return; }
                ChooseOwnCards(owner, "骁果：弃一基本手牌", 1, 1, false, cards =>
                {
                    PayCard(owner, cards[0]); EmitSkill(owner, "骁果"); List<Card> equipment = He(actor).Where(card => card.IsEquipment).ToList(); List<SkillDecisionOption> options = equipment.Select(card => new SkillDecisionOption(card.Id.ToString(), "弃装备 · " + CardLabel(card))).ToList(); options.Add(new("damage", "受到1点伤害"));
                    Decide(actor, "骁果：弃一装备牌，否则受到伤害", options, equipment.Count > 0 ? equipment.MinBy(KeepValue)!.Id.ToString() : "damage", key =>
                    { if (key == "damage") Damage(owner, actor, null, () => Xiaoguo(index + 1)); else { PayCard(actor, equipment.First(card => card.Id.ToString() == key)); AfterLosses(() => Xiaoguo(index + 1)); } });
                }, card => card.IsBasic);
            });
        }
        Xiaoguo(0);
    }
    private void BatchThreeVictimDamage(Player? source, Player victim, Card? card, int amount, Action done)
    {
        if (((victim.RuleId == "caopi" && victim.HasAbility("放逐"))))
        {
            int lost = victim.MaxHealth - victim.Health;
            ChoosePlayer(victim, "放逐：可令其他角色摸已损体力张并翻面", Players.Where(player => player != victim), true,
                target => target.TurnedOver ? (Allied(victim, target) ? 100 : -100) : Hostility(victim, target) > 0 ? 6 - lost : lost >= 4 ? lost : -1, target =>
                { if (target != null) { Draw(target, lost); TurnOver(target); EmitSkill(victim, "放逐"); } done(); }); return;
        }
        if (((victim.RuleId == "xunyu" && victim.HasAbility("节命"))))
        {
            void Next(int index)
            {
                if (index >= amount || !victim.IsAlive) { done(); return; }
                ChoosePlayer(victim, "节命：可令一人补牌至体力上限，至多五张", Players, true,
                    player => Allied(victim, player) ? Math.Min(5, player.MaxHealth) - player.Hand.Count : -1, target =>
                    { if (target != null) { Draw(target, Math.Max(0, Math.Min(5, target.MaxHealth) - target.Hand.Count)); EmitSkill(victim, "节命"); } Next(index + 1); });
            }
            Next(0); return;
        }
        done();
    }
    private void AfterBatchThreeDamage(Player? source, Player victim, Card? card, CardKind? kind, int amount, Action done)
    {
        void Lords()
        {
            Player? lord = Players.FirstOrDefault(player => player.IsAlive && ((player.RuleId == "dongzhuo" && player.HasAbility("暴虐"))) && Lord(player) && player != source);
            if (source?.IsAlive != true || source.Kingdom != "群" || lord == null) { done(); return; }
            OptionalSkill(source, $"暴虐：是否判定，黑桃令主公{lord.Hero.Name}回复？", Allied(source, lord) && lord.Health < lord.MaxHealth, use =>
            { if (!use) done(); else Judge(source, "暴虐", judged => { EmitSkill(lord, "暴虐"); if (judged != null && SuitFor(source, judged) == "♠") HealWithChoices(lord, done); else done(); }); });
        }
        void Kuangfu()
        {
            if (source?.IsAlive == true && ((source.RuleId == "panfeng" && source.HasAbility("狂斧"))) && source == Current && source.InPlayPhase && kind == CardKind.Slash && victim.IsAlive && source != victim && source.Mark("panfengUsed") == 0)
            { source.SetMark("panfengUsed", 1); EmitSkill(source, "狂斧"); if (victim.Health < source.Health) { Draw(source, 2); Lords(); } else LoseHealth(source, 1, Lords); }
            else Lords();
        }
        List<Player> owners = new[] { source, victim }.Where(player => player?.IsAlive == true && ((player.RuleId == "lidian" && player.HasAbility("忘隙")))).Cast<Player>().Distinct().ToList();
        void Wangxi(int index, int point)
        {
            if (source?.IsAlive != true || !victim.IsAlive || source == victim || index >= owners.Count) { Kuangfu(); return; }
            if (point >= amount) { Wangxi(index + 1, 0); return; }
            Player owner = owners[index], other = owner == source ? victim : source;
            OptionalSkill(owner, $"忘隙：是否与你的另一方{other.Hero.Name}各摸一张？", Allied(owner, other) || owner.Hand.Count < 2, use =>
            { if (use) { Draw(owner, 1); Draw(other, 1); EmitSkill(owner, "忘隙"); } Wangxi(index, point + 1); });
        }
        Wangxi(0, 0);
    }
    private void QueueTuntian(Player player)
    {
        if (!((player.RuleId == "dengai" && player.HasAbility("屯田"))) || player == Current || !player.IsAlive || !pendingTuntian.Add(player.Seat)) return;
        reactions.Enqueue(done =>
        {
            pendingTuntian.Remove(player.Seat); if (!player.IsAlive || !((player.RuleId == "dengai" && player.HasAbility("屯田")))) { done(); return; }
            OptionalSkill(player, "屯田：回合外失牌，是否判定？非红桃作为田", true, use =>
            { if (!use) done(); else Judge(player, "屯田", card => { if (card != null && SuitFor(player, card) != "♥" && DiscardPile.Remove(card)) player.Zone("田").Add(card); EmitSkill(player, "屯田"); done(); }); });
        });
    }
    private void Leiji(Player player, Action done)
    {
        ChoosePlayer(player, "雷击：可令一名角色判定，黑桃受到2雷电伤害", Players, true, target => Hostility(player, target), target =>
        { if (target == null) { done(); return; } EmitSkill(player, "雷击"); Judge(target, "雷击", card => { if (card != null && SuitFor(target, card) == "♠") ElementalDamage(player, target, null, 2, DamageNature.Thunder, done); else done(); }); });
    }
    private void EliminateWithTriggers(Player? source, Player victim, Action done)
    {
        Player? caopi = Players.FirstOrDefault(player => player.IsAlive && player != victim && ((player.RuleId == "caopi" && player.HasAbility("行殇"))));
        if (caopi == null || !He(victim).Any()) { Eliminate(source, victim); done(); return; }
        OptionalSkill(caopi, $"行殇：获得{victim.Hero.Name}的全部手牌和装备？", true, use =>
        { if (use) { Gain(caopi, He(victim)); victim.Hand.Clear(); victim.Equipment.Clear(); EmitSkill(caopi, "行殇"); } Eliminate(source, victim); done(); });
    }
    private void JizhiDraw(Player player, Action done)
    {
        HashSet<int> before = player.Hand.Select(card => card.Id).ToHashSet(); Draw(player, 1); EmitSkill(player, "集智");
        Card? gained = player.Hand.FirstOrDefault(card => !before.Contains(card.Id));
        if (!((player.SkillIdentity == "re_huangyueying" && player.HasAbility("集智"))) || gained?.IsBasic != true) { done(); return; }
        OptionalSkill(player, "界集智：弃掉刚摸的基本牌，本回合手牌上限+1？", player == Current && player.Hand.Count > player.HandLimit + 2 && KeepValue(gained) < 6, use =>
        { if (use) { PayCard(player, gained); player.SetMark("handBonus", player.Mark("handBonus") + 1); } done(); });
    }
}
