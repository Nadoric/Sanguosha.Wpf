namespace Sanguosha.Core;
public sealed partial class GameEngine
{
    private bool ExecuteBatchThreeSkill(Player actor, Card? card, Player? target, Player? second)
    {
        if (!actor.HasActiveAbility) return false;
        if (actor.SkillsDisabled) return false;
        Action done = () => CompleteAction(actor);
        if (((actor.RuleId == "xunyu" && actor.HasAbility("驱虎"))))
        {
            if (actor.SkillUsed || actor.Hand.Count == 0 || target == null || target == actor || target.Hand.Count == 0 || target.Health <= actor.Health) return false;
            actor.SkillUsed = true; Phase = GamePhase.Resolving; EmitSkill(actor, "驱虎"); CompareHands(actor, target, won =>
            {
                if (!won) { Damage(target, actor, null, done); return; }
                ChoosePlayer(actor, "驱虎胜：选择拼点对象攻击范围内的伤害承受者", Players.Where(player => player != target && Distance(target, player) <= target.AttackRange), false, player => Hostility(actor, player), victim =>
                { if (victim == null) done(); else Damage(target, victim, null, done); });
            }); return true;
        }
        if (((actor.RuleId == "yuanshao" && actor.HasAbility("乱击"))))
        {
            List<Card> valid = actor.Hand.Where(item => actor.Hand.Count(other => SuitFor(actor, other) == SuitFor(actor, item)) >= 2).ToList();
            if (valid.Count < 2) return false; Phase = GamePhase.Resolving;
            Decide(actor, "乱击：选择第一张手牌", valid.Select(item => new SkillDecisionOption(item.Id.ToString(), CardLabel(item), PreviewCard: item)).ToList(), valid.MinBy(KeepValue)!.Id.ToString(), key =>
            {
                Card first = valid.First(item => item.Id.ToString() == key);
                ChooseOwnCards(actor, "乱击：选择另一张同花色手牌", 1, 1, false, cards =>
                    VirtualCard(actor, CardKind.Arrows, new() { first, cards[0] }, Players.Where(player => player.IsAlive && player != actor).OrderBy(player => (player.Seat - actor.Seat + Players.Count) % Players.Count).ToList(), "乱击", done), item => item != first && SuitFor(actor, item) == SuitFor(actor, first));
            }); return true;
        }
        if (((actor.RuleId == "wolong" && actor.HasAbility("火计"))))
        {
            if (target == null || target.Hand.Count == 0) return false;
            List<Card> red = actor.Hand.Where(item => RedFor(actor, item)).ToList(); if (red.Count == 0) return false;
            Phase = GamePhase.Resolving; ChooseOwnCards(actor, "火计：选择红色手牌当火攻", 1, 1, false, cards => VirtualCard(actor, CardKind.FireAttack, cards, new() { target }, "火计", done), item => RedFor(actor, item)); return true;
        }
        if (((actor.RuleId == "dongzhuo" && actor.HasAbility("酒池"))))
        {
            if (actor.Mark("wineUsed") > 0 || !actor.Hand.Any(item => SuitFor(actor, item) == "♠")) return false;
            Phase = GamePhase.Resolving; ChooseOwnCards(actor, "酒池：一张黑桃手牌当酒", 1, 1, false, cards =>
            { VirtualCard(actor, CardKind.Wine, cards, new() { actor }, "酒池", done); }, item => SuitFor(actor, item) == "♠"); return true;
        }
        if (((actor.RuleId == "jiaxu" && actor.HasAbility("乱武"))))
        {
            if (actor.LimitedUsed) return false; actor.LimitedUsed = true; Phase = GamePhase.Resolving; EmitSkill(actor, "乱武");
            List<Player> actors = Enumerable.Range(1, Players.Count - 1).Select(offset => Players[(actor.Seat + offset) % Players.Count]).Where(player => player.IsAlive).ToList();
            void Next(int index)
            {
                if (index >= actors.Count || Phase == GamePhase.GameOver) { done(); return; }
                Player current = actors[index]; if (!current.IsAlive) { Next(index + 1); return; }
                int minimum = Players.Where(player => player.IsAlive && player != current).Min(player => Distance(current, player));
                List<Player> targets = Players.Where(player => player.IsAlive && player != current && Distance(current, player) == minimum  && !(((player.RuleId == "zhugeliang" && player.HasAbility("空城"))) && player.Hand.Count == 0)).ToList();
                ForceSlash(current, targets, "乱武", false, success => { if (success) Next(index + 1); else LoseHealth(current, 1, () => Next(index + 1)); });
            }
            Next(0); return true;
        }
        if (((actor.RuleId == "dengai" && actor.HasAbility("急袭"))))
        {
            if (!actor.Awakened || actor.Zone("田").Count == 0 || target == null || target == actor || He(target).Count() + target.Judgments.Count == 0 || ((target.RuleId == "luxun" && target.HasAbility("谦逊")))) return false;
            List<Card> choices = actor.Zone("田").ToList().Where(item => { actor.Zone("田").Remove(item); bool legal = Distance(actor, target) <= 1; actor.Zone("田").Add(item); return legal && !(((target.RuleId == "jiaxu" && target.HasAbility("乱武"))) && !item.IsRed); }).ToList();
            if (choices.Count == 0) return false; Phase = GamePhase.Resolving;
            Decide(actor, "急袭：选择一张田当顺手牵羊（移去此田后计算距离）", choices.Select(item => new SkillDecisionOption(item.Id.ToString(), CardLabel(item), PreviewCard: item)).ToList(), choices.MinBy(KeepValue)!.Id.ToString(), key =>
                VirtualCard(actor, CardKind.Steal, new() { choices.First(item => item.Id.ToString() == key) }, new() { target }, "急袭", done)); return true;
        }
        if (((actor.RuleId == "liushan" && actor.HasAbility("激将")))) return UseJijiang(actor, target);
        if (((actor.RuleId == "yiji" && actor.HasAbility("机捷"))))
        {
            if (actor.Mark("jijieUsed") > 0) return false;
            if (deck.Count == 0) { Card? refill = TakeTop(); if (refill != null) deck.Add(refill); }
            if (deck.Count == 0) return false;
            actor.SetMark("jijieUsed", 1); Phase = GamePhase.Resolving; Card bottom = deck[0]; deck.RemoveAt(0); OrderingCards.Add(bottom);
            ChoosePlayer(actor, $"机捷：{CardLabel(bottom)}，选择获得牌的角色（仅你可见）", Players, false, player => Allied(actor, player) ? 10 + 5 - player.Hand.Count : -10, recipient =>
            { OrderingCards.Remove(bottom); Gain(recipient!, bottom); EmitSkill(actor, "机捷"); if (recipient != actor) Jiyuan(actor, recipient!, done); else done(); }); return true;
        }
        if (((actor.SkillIdentity == "re_huatuo" && actor.HasAbility("青囊"))))
        {
            if (actor.Mark("qingnangOff") > 0 || card == null || target == null || target.Health >= target.MaxHealth || target.Mark("qingnangTarget") > 0) return false;
            Phase = GamePhase.Resolving; target.SetMark("qingnangTarget", 1); if (!RedFor(actor, card)) actor.SetMark("qingnangOff", 1); PayCard(actor, card); EmitSkill(actor, "青囊"); HealWithChoices(target, done); return true;
        }
        if (((actor.SkillIdentity == "re_daqiao" && actor.HasAbility("国色"))))
        {
            if (actor.SkillUsed || target == null) return false;
            List<Card> costs = He(actor).Where(item => SuitFor(actor, item) == "♦").ToList();
            Card? delayed = target.Judgments.FirstOrDefault(item => DelayedKind(target, item) == CardKind.Indulgence);
            if (costs.Count == 0 || delayed == null && (target == actor || ((target.RuleId == "luxun" && target.HasAbility("谦逊"))))) return false;
            Phase = GamePhase.Resolving; ChooseOwnCards(actor, "界国色：选择方块手牌/装备", 1, 1, true, cards =>
            {
                Card cost = cards[0]; actor.SkillUsed = true; if (delayed == null) reservedUseMaterials.Add(cost.Id); PayCard(actor, cost); EmitSkill(actor, "国色");
                if (delayed != null) { target.Judgments.Remove(delayed); DiscardPile.Add(delayed); Draw(actor, 1); done(); }
                else AfterLosses(() => BeginCardUse(actor, CardKind.Indulgence, cards, new List<Player> { target }, finish => { DiscardPile.Remove(cost); target.Judgments.Add(cost); finish(); }, () => { Draw(actor, 1); done(); }));
            }, item => SuitFor(actor, item) == "♦"); return true;
        }
        if (((actor.SkillIdentity == "re_sunquan" && actor.HasAbility("制衡"))))
        {
            if (actor.SkillUsed || !He(actor).Any()) return false;
            Phase = GamePhase.Resolving; List<Card> hand = actor.Hand.ToList();
            ChooseOwnCards(actor, "界制衡：弃任意手牌/装备，全弃手牌额外摸一", 1, He(actor).Count(), true, cards =>
            { actor.SkillUsed = true; bool allHand = hand.Count > 0 && hand.All(cards.Contains); PayCards(actor, cards); Draw(actor, cards.Count + (allHand ? 1 : 0)); EmitSkill(actor, "制衡"); done(); }); return true;
        }
        if (((actor.SkillIdentity == "re_liubei" && actor.HasAbility("仁德"))))
        {
            if (card == null) return UseJijiang(actor, target);
            if (target == null || target == actor || actor.Mark("rendeTo" + target.Seat) > 0) return false;
            Phase = GamePhase.Resolving;
            ChooseOwnCards(actor, "界仁德：选择赠给该角色的手牌，本阶段各目标限一次", 1, actor.Hand.Count, false, cards =>
            {
                actor.SetMark("rendeTo" + target.Seat, 1); int before = actor.RendeGiven; actor.RendeGiven += cards.Count;
                GiveCards(actor, target, cards); EmitSkill(actor, "仁德");
                if (before < 2 && actor.RendeGiven >= 2) FreeBasic(actor, done); else done();
            }); return true;
        }
        if (((actor.SkillIdentity == "re_lvmeng" && actor.HasAbility("攻心"))) && actor.Awakened)
        {
            if (actor.SkillUsed || target == null || target == actor || target.Hand.Count == 0) return false;
            actor.SkillUsed = true; Phase = GamePhase.Resolving;
            List<Card> hearts = target.Hand.Where(item => SuitFor(target, item) == "♥").ToList();
            List<SkillDecisionOption> options = target.Hand.Select(item => new SkillDecisionOption(hearts.Contains(item) ? item.Id.ToString() : "view:" + item.Id, (hearts.Contains(item) ? "选择红桃 · " : "仅查看 · ") + CardLabel(item), PreviewCard: item)).ToList(); options.Add(new("no", "只观看，不处理牌"));
            Decide(actor, "攻心：观看目标手牌，可选择红桃弃置或置顶（仅你可见）", options, hearts.Count > 0 ? hearts.MaxBy(KeepValue)!.Id.ToString() : "no", key =>
            {
                if (key == "no" || key.StartsWith("view:")) { done(); return; } Card chosen = hearts.First(item => item.Id.ToString() == key); ShowCard(chosen);
                Decide(actor, "攻心：处理此红桃牌", new[] { new SkillDecisionOption("discard", "弃置"), new SkillDecisionOption("top", "置于牌堆顶") }, "discard", mode =>
                { target.Hand.Remove(chosen); if (mode == "discard") DiscardPile.Add(chosen); else deck.Add(chosen); OnHandLost(target); EmitSkill(actor, "攻心"); done(); });
            }); return true;
        }
        return false;
    }
    private void FreeBasic(Player actor, Action done)
    {
        List<SkillDecisionOption> options = new() { new("no", "不使用基本牌") };
        if (actor.Health < actor.MaxHealth) options.Add(new("peach", "视为使用桃"));
        if (actor.Mark("wineUsed") == 0) options.Add(new("wine", "视为使用酒"));
        List<Player> targets = Players.Where(player => player.IsAlive && player != actor && !(((player.RuleId == "zhugeliang" && player.HasAbility("空城"))) && player.Hand.Count == 0) && Distance(actor, player) <= actor.AttackRange).ToList();
        if ((actor.SlashCount == 0 || actor.Equipment.Any(item => item.Kind == CardKind.Crossbow)) && targets.Count > 0) options.AddRange(new[] { new SkillDecisionOption("slash", "视为普通杀"), new SkillDecisionOption("fire", "视为火杀"), new SkillDecisionOption("thunder", "视为雷杀") });
        Decide(actor, "界仁德累计二张：可视为使用一张基本牌", options, actor.Health < actor.MaxHealth ? "peach" : options.Any(option => option.Key == "slash") && targets.Any(player => Hostility(actor, player) > 0) ? "slash" : "no", key =>
        {
            if (key == "no") { done(); return; }
            if (key == "peach") { VirtualBasic(actor, CardKind.Peach, DamageNature.Normal, null, done); return; }
            if (key == "wine") { VirtualBasic(actor, CardKind.Wine, DamageNature.Normal, null, done); return; }
            ChoosePlayer(actor, "界仁德：基本杀的目标", targets, false, player => Hostility(actor, player), target => VirtualBasic(actor, CardKind.Slash, key == "fire" ? DamageNature.Fire : key == "thunder" ? DamageNature.Thunder : DamageNature.Normal, target, done));
        });
    }
    private void VirtualBasic(Player actor, CardKind kind, DamageNature nature, Player? target, Action done)
    {
        Card card = new(-(++useNumber), kind, "", 0, nature); usedSlashNatures[card.Id] = nature;
        EmitCue(GameCueKind.CardPlayed, actor, kind, target: target, nature: nature);
        BeginCardUse(actor, kind, Array.Empty<Card>(), new[] { target ?? actor }, finish => ResolveCardEffect(actor, card, target, kind, finish), done);
    }
    private void ForceSlash(Player actor, List<Player> targets, string reason, bool useQuota, Action<bool> done)
    {
        List<(Card Card, Player Target)> legal = new();
        foreach (Card card in He(actor).Where(item => CanTransform(actor, item, ResponseKind.Slash) && (actor.Hand.Contains(item) || ((actor.RuleId == "guanyu" && actor.HasAbility("武圣"))))))
        foreach (Player target in targets)
        {
            int range = actor.Equipment.Contains(card) && EquipmentSlot(card) == 0 ? 1 : actor.AttackRange;
            if ((Distance(actor, target) <= range || actor.SkillIdentity == "re_guanyu" && actor.HasAbility("武圣") && SuitFor(actor, card) == "♦") && !(((target.RuleId == "zhugeliang" && target.HasAbility("空城"))) && target.Hand.Count == 0)) legal.Add((card, target));
        }
        List<SkillDecisionOption> options = legal.Select(pair => new SkillDecisionOption($"{pair.Card.Id}:{pair.Target.Seat}", $"{CardLabel(pair.Card)} → {pair.Target.Hero.Name}", PreviewCard: pair.Card)).ToList(); if (CanGuhuo(actor)) options.Add(new("guhuo", "蛊惑声明杀")); options.Add(new("no", "不使用杀"));
        var best = legal.OrderByDescending(pair => Hostility(actor, pair.Target)).ThenBy(pair => KeepValue(pair.Card)).FirstOrDefault();
        Decide(actor, reason + "：选择杀及目标", options, best.Card != null && Hostility(actor, best.Target) > 0 ? $"{best.Card.Id}:{best.Target.Seat}" : "no", key =>
        {
            if (key == "no") { done(false); return; }
            if (key == "guhuo")
            {
                List<Player> available = targets.Where(target => target.IsAlive && Distance(actor, target) <= actor.AttackRange && !(target.HasAbility("空城") && target.Hand.Count == 0)).ToList();
                if (available.Count == 0) { done(false); return; }
                ChoosePlayer(actor, reason + " · 蛊惑杀目标", available, false, target => Hostility(actor, target), chosen =>
                    Decide(actor, "蛊惑：声明杀的属性", GuhuoClaims().Where(claim => claim.Kind == CardKind.Slash).Select(claim => new SkillDecisionOption(ClaimKey(claim), claim.Name)).ToList(), "Slash:Normal", declaration =>
                        ChooseOwnCards(actor, "蛊惑：秘密选择杀的材料", 1, 1, false, cards =>
                            GuhuoAttempt(actor, cards[0], GuhuoClaims().First(claim => ClaimKey(claim) == declaration), new() { chosen! }, (effective, declared) =>
                            {
                                if (!effective) { work.Enqueue(() => ForceSlash(actor, targets, reason, useQuota, done)); return; }
                                if (useQuota) actor.SlashCount++; actor.PlayedSlash |= actor == Current && actor.InPlayPhase; usedSlashNatures[declared.Id] = declared.Nature;
                                actor.SetMark("wineSlash", actor.Mark("wine")); actor.SetMark("wine", 0);
                                EmitCue(GameCueKind.CardPlayed, actor, CardKind.Slash, target: chosen, physicalCard: declared);
                                BeginCardUse(actor, CardKind.Slash, cards, new[] { chosen! }, finish => DeclaredSlash(actor, chosen!, declared, finish), () => { actor.SetMark("wineSlash", 0); done(true); });
                            })))); return;
            }
            var pair = legal.First(pair => $"{pair.Card.Id}:{pair.Target.Seat}" == key); reservedUseMaterials.Add(pair.Card.Id); PayCard(actor, pair.Card); usedSlashNatures[pair.Card.Id] = pair.Card.Kind == CardKind.Slash ? pair.Card.Nature : DamageNature.Normal;
            if (useQuota) actor.SlashCount++; actor.PlayedSlash |= actor == Current && actor.InPlayPhase;
            actor.SetMark("wineSlash", actor.Mark("wine")); actor.SetMark("wine", 0);
            EmitCue(GameCueKind.CardPlayed, actor, CardKind.Slash, target: pair.Target, physicalCard: pair.Card);
            AfterLosses(() => BeginCardUse(actor, CardKind.Slash, new[] { pair.Card }, new[] { pair.Target }, finish => DeclaredSlash(actor, pair.Target, pair.Card, finish), () => { actor.SetMark("wineSlash", 0); done(true); }));
        });
    }
    private void Jiyuan(Player owner, Player target, Action done)
    {
        if (!owner.HasAbility("急援")) { done(); return; }
        OptionalSkill(owner, $"急援：是否令{target.Hero.Name}摸一张牌？", Allied(owner, target), use => { if (use) { Draw(target, 1); EmitSkill(owner, "急援"); } done(); });
    }
}
