namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private bool ExecuteBatchSkill(Player actor, Card? card, Player? target, Player? second)
    {
        if (!actor.HasActiveAbility) return false;
        Action done = () => CompleteAction(actor);
        if (((actor.RuleId == "zhangzhaozhanghong" && actor.HasAbility("直谏"))))
        {
            if (card?.IsEquipment != true || target == null || target == actor || target.Equipment.Any(item => EquipmentSlot(item) == EquipmentSlot(card))) return false;
            actor.Hand.Remove(card); target.Equipment.Add(card); OnHandLost(actor); Draw(actor, 1); EmitSkill(actor, "直谏"); done(); return true;
        }
        if (((actor.RuleId == "pangtong" && actor.HasAbility("连环"))))
        {
            if (card == null || card.Suit != "♣") return false;
            Phase = GamePhase.Resolving;
            ResolveIronChain(actor, card, done, false, "连环"); return true;
        }
        if (((actor.RuleId == "taishici" && actor.HasAbility("天义"))))
        {
            if (actor.SkillUsed || actor.Hand.Count == 0 || target == null || target == actor || target.Hand.Count == 0) return false;
            actor.SkillUsed = true; Phase = GamePhase.Resolving; EmitSkill(actor, "天义");
            CompareHands(actor, target, won => { actor.TianyiState = won ? 1 : -1; done(); }); return true;
        }
        if (((actor.RuleId == "jiangwei" && actor.HasAbility("挑衅"))))
        {
            if (actor.SkillUsed || target == null || target == actor || Distance(target, actor) > target.AttackRange) return false;
            actor.SkillUsed = true; Phase = GamePhase.Resolving; EmitSkill(actor, "挑衅");
            ForceSlash(target, new() { actor }, "挑衅", false, used =>
            { if (!used && He(target).Any()) ChooseOtherCard(actor, target, false, done); else done(); }); return true;
        }
        if (((actor.RuleId == "lusu" && actor.HasAbility("缔盟"))))
        {
            if (actor.SkillUsed || target == null || second == null || target == actor || second == actor || target == second) return false;
            int cost = Math.Abs(target.Hand.Count - second.Hand.Count);
            if (He(actor).Count() < cost) return false;
            Phase = GamePhase.Resolving;
            ChooseOwnCards(actor, "缔盟：弃置手牌或装备", cost, cost, true, cards =>
            {
                PayCards(actor, cards);
                List<Card> firstHand = target.Hand.ToList(), secondHand = second.Hand.ToList();
                target.Hand.Clear(); second.Hand.Clear(); Gain(target, secondHand); Gain(second, firstHand);
                OnHandLost(target, firstHand.Count); OnHandLost(second, secondHand.Count); actor.SkillUsed = true; EmitSkill(actor, "缔盟"); done();
            }); return true;
        }
        if (((actor.RuleId == "re_sunshangxiang" && actor.HasAbility("结姻"))))
        {
            if (actor.SkillUsed || target == null || target == actor || target.IsFemale) return false;
            List<SkillDecisionOption> options = actor.Hand.Select(item => new SkillDecisionOption($"discard:{item.Id}", "弃手牌 · " + CardLabel(item))).ToList();
            options.AddRange(He(actor).Where(item => item.IsEquipment && !target.Equipment.Any(other => EquipmentSlot(other) == EquipmentSlot(item)))
                .Select(item => new SkillDecisionOption($"equip:{item.Id}", "置入目标装备区 · " + CardLabel(item))));
            if (options.Count == 0) return false;
            Phase = GamePhase.Resolving;
            Decide(actor, "界结姻：选择手牌代价或交出装备", options, options[0].Key, key =>
            {
                string[] parts = key.Split(':'); Card selected = He(actor).First(item => item.Id == int.Parse(parts[1]));
                if (parts[0] == "discard") PayCard(actor, selected);
                else { bool equipped = actor.Equipment.Remove(selected); actor.Hand.Remove(selected); target.Equipment.Add(selected); if (equipped) OnEquipmentLost(actor); else OnHandLost(actor); }
                AfterLosses(() =>
                {
                actor.SkillUsed = true; EmitSkill(actor, "结姻");
                if (actor.Health == target.Health) done();
                else { Player higher = actor.Health > target.Health ? actor : target; Player lower = higher == actor ? target : actor; Draw(higher, 1); HealWithChoices(lower, done); }
                });
            }); return true;
        }
        return false;
    }

    private void HealWithChoices(Player player, Action done)
    {
        Player? lord = Players.FirstOrDefault(other => other.IsAlive && other != player && ((other.SkillIdentity == "re_sunquan" && other.HasAbility("救援"))) && !other.SkillsDisabled && Lord(other) && other.Health <= player.Health);
        if (lord != null && player == Current && player.Kingdom == "吴" && player.Health < player.MaxHealth)
        {
            OptionalSkill(player, $"界救援：改为主公{lord.Hero.Name}回复1，你摸1？", Allied(player, lord) && lord.Health < lord.MaxHealth, use =>
            { if (!use) HealWithChoicesCore(player, done); else { EmitSkill(lord, "救援"); HealWithChoicesCore(lord, () => { Draw(player, 1); done(); }); } }); return;
        }
        HealWithChoicesCore(player, done);
    }
    private void HealWithChoicesCore(Player player, Action done)
    {
        int before = player.Health; Heal(player);
        void Shushen(Action next)
        {
            if (!((player.RuleId == "ganfuren" && player.HasAbility("淑慎"))) || player.Health <= before) { next(); return; }
            ChoosePlayer(player, "淑慎：可令他人摸一，空手则摸二", Players.Where(other => other != player), true, other => -Hostility(player, other), target => { if (target != null) { Draw(target, target.Hand.Count == 0 ? 2 : 1); EmitSkill(player, "淑慎"); } next(); });
        }
        Action original = done; done = () => Shushen(original);
        int keep = Math.Max(0, 1 - player.Health);
        void Remove()
        {
            if (!((player.RuleId == "zhoutai" && player.HasAbility("不屈"))) || player.SpecialCards.Count <= keep) { done(); return; }
            if (keep == 0) { DiscardPile.AddRange(player.SpecialCards); player.SpecialCards.Clear(); done(); return; }
            Card best = player.SpecialCards.GroupBy(card => card.Rank).OrderByDescending(group => group.Count()).First().First();
            Decide(player, "不屈：回复体力后选择移去一张不屈牌", player.SpecialCards.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card))).ToList(), best.Id.ToString(), key =>
            { Card card = player.SpecialCards.First(card => card.Id.ToString() == key); player.SpecialCards.Remove(card); DiscardPile.Add(card); Remove(); });
        }
        Remove();
    }

    private bool IsBuquAlive(Player player) => ((player.RuleId == "zhoutai" && player.HasAbility("不屈"))) && player.SpecialCards.Count == Math.Max(0, 1 - player.Health) &&
        player.SpecialCards.Select(card => card.Rank).Distinct().Count() == player.SpecialCards.Count;

    private void TryLiyu(Player? source, Player victim, CardKind? kind, Action done)
    {
        if (!((source?.RuleId == "re_lvbu" && source?.HasAbility("利驭") == true)) || kind != CardKind.Slash || !victim.IsAlive || !He(victim).Any()) { done(); return; }
        ChoosePlayer(victim, "利驭：可令界吕布获得你一张牌，并对所选第三人决斗", Players.Where(player => player != source && player != victim && !(((player.RuleId == "zhugeliang" && player.HasAbility("空城"))) && player.Hand.Count == 0)), true,
            player => Hostility(victim, player) > 0 ? Hostility(victim, player) - 40 : -100, target =>
            { if (target == null) done(); else { EmitSkill(source, "利驭"); ChooseOtherCard(source, victim, true, () => StartVirtualDuel(source, target, done)); } });
    }

    private void Beige(Player? source, Player victim, CardKind? kind, Action done)
    {
        List<Player> owners = Players.Where(player => player.IsAlive && ((player.RuleId == "caiwenji" && player.HasAbility("悲歌"))) && He(player).Any()).ToList();
        void Next(int index)
        {
            if (kind != CardKind.Slash || index >= owners.Count || Phase == GamePhase.GameOver) { done(); return; }
            Player owner = owners[index];
            OptionalSkill(owner, $"悲歌：是否弃一牌令{victim.Hero.Name}判定？", Allied(owner, victim), use =>
            {
                if (!use) { Next(index + 1); return; }
                ChooseOwnCards(owner, "悲歌：选择弃置牌", 1, 1, true, cards =>
                {
                    PayCard(owner, cards[0]); EmitSkill(owner, "悲歌");
                    Judge(victim, "悲歌", card =>
                    {
                        Action next = () => Next(index + 1);
                        switch (card == null ? "" : SuitFor(victim, card))
                        {
                            case "♥" when victim.IsAlive: HealWithChoices(victim, next); break;
                            case "♦" when victim.IsAlive: Draw(victim, 2); next(); break;
                            case "♣" when source?.IsAlive == true: DiscardOwnCards(source, "悲歌：弃两张牌", 2, true, next); break;
                            case "♠" when source?.IsAlive == true: TurnOver(source); next(); break;
                            default: next(); break;
                        }
                    });
                });
            });
        }
        Next(0);
    }

    private Player? discardingActor;
    private readonly List<Card> phaseDiscards = new();
    private void FinishGuzheng(Player actor, Action done)
    {
        discardingActor = null;
        Player? owner = Players.FirstOrDefault(player => player.IsAlive && player != actor && ((player.RuleId == "zhangzhaozhanghong" && player.HasAbility("固政"))));
        List<Card> cards = phaseDiscards.Where(DiscardPile.Contains).Distinct().ToList(); phaseDiscards.Clear();
        if (owner == null || cards.Count == 0 || !actor.IsAlive) { done(); return; }
        OptionalSkill(owner, "固政：将一张弃置手牌归还弃牌者，并获得其余弃牌？", cards.Count > 1 || Allied(owner, actor), use =>
        {
            if (!use) { done(); return; }
            Decide(owner, $"固政：归还{actor.Hero.Name}一张牌", cards.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card))).ToList(), cards.MinBy(KeepValue)!.Id.ToString(), key =>
            {
                Card returned = cards.First(card => card.Id.ToString() == key); if (DiscardPile.Remove(returned)) Gain(actor, returned);
                foreach (Card card in cards.Where(card => card != returned)) if (DiscardPile.Remove(card)) Gain(owner, card);
                EmitSkill(owner, "固政"); done();
            });
        });
    }
}
