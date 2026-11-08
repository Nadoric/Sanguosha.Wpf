namespace Sanguosha.Core;
public sealed partial class GameEngine
{
    private bool TryBatchThreeAi(Player actor)
    {
        List<Player> enemies = Players.Where(player => player.IsAlive && player != actor && Hostility(actor, player) > 0).OrderByDescending(player => Hostility(actor, player) + 6 * (5 - player.Health)).ToList();
        foreach (var global in GlobalActive(actor))
        {
            if (!Allied(actor, global.Lord)) continue;
            if (global.Key == "huangtian" && actor.Hand.Count >= 2 || global.Key == "zhiba" && actor.Hand.Count > actor.Health)
            { ExecuteGlobal(actor, global.Key, global.Lord, () => CompleteAction(actor)); return true; }
        }
        if (((actor.RuleId == "yiji" && actor.HasAbility("机捷"))) && actor.Mark("jijieUsed") == 0) return ExecuteBatchThreeSkill(actor, null, null, null);
        if (((actor.RuleId == "xunyu" && actor.HasAbility("驱虎"))) && !actor.SkillUsed && actor.Hand.Any(card => card.Rank >= 10))
        {
            Player? target = enemies.FirstOrDefault(player => player.Health > actor.Health && player.Hand.Count > 0);
            if (target != null && (actor.Health > 1 || actor.Hand.Any(card => card.Rank == 13))) return ExecuteBatchThreeSkill(actor, null, target, null);
        }
        if (((actor.RuleId == "dengai" && actor.HasAbility("急袭"))) && actor.Awakened && actor.Zone("田").Count > 0)
            foreach (Player target in enemies) if (ExecuteBatchThreeSkill(actor, null, target, null)) return true;
        if (((actor.RuleId == "jiaxu" && actor.HasAbility("乱武"))) && !actor.LimitedUsed && Players.Where(player => player.IsAlive && player != actor).Sum(player => Hostility(actor, player) / Math.Max(1, player.Health)) > 60)
            return ExecuteBatchThreeSkill(actor, null, null, null);
        if (((actor.RuleId == "yuanshao" && actor.HasAbility("乱击"))) && actor.Hand.GroupBy(card => SuitFor(actor, card)).Any(group => group.Count() >= 2) && enemies.Count > Players.Count(player => player.IsAlive && player != actor && Allied(actor, player)))
            return ExecuteBatchThreeSkill(actor, null, null, null);
        if (((actor.RuleId == "wolong" && actor.HasAbility("火计"))) && actor.Hand.Count >= 2 && actor.Hand.Any(card => RedFor(actor, card) && KeepValue(card) < 7))
        { Player? target = enemies.FirstOrDefault(player => player.Hand.Count > 0); if (target != null) return ExecuteBatchThreeSkill(actor, null, target, null); }
        if (((actor.RuleId == "dongzhuo" && actor.HasAbility("酒池"))) && actor.Mark("wineUsed") == 0 && actor.Hand.Any(card => card.Suit == "♠") && actor.Hand.Any(card => card.Kind == CardKind.Slash) && actor.SlashCount == 0)
            return ExecuteBatchThreeSkill(actor, null, null, null);
        if (!actor.SkillsDisabled && ((actor.SkillIdentity == "re_huatuo" && actor.HasAbility("青囊"))) && actor.Mark("qingnangOff") == 0)
        {
            Player? target = Players.FirstOrDefault(player => player.IsAlive && Allied(actor, player) && player.Health < player.MaxHealth && player.Mark("qingnangTarget") == 0);
            Card? cost = actor.Hand.OrderBy(card => card.IsRed ? 0 : 1).ThenBy(KeepValue).FirstOrDefault();
            if (target != null && cost != null) return ExecuteBatchThreeSkill(actor, cost, target, null);
        }
        if (!actor.SkillsDisabled && ((actor.SkillIdentity == "re_daqiao" && actor.HasAbility("国色"))) && !actor.SkillUsed && He(actor).Any(card => card.Suit == "♦"))
        {
            foreach (Player target in Players.Where(player => player.IsAlive && Allied(actor, player) && player.Judgments.Any(card => DelayedKind(player, card) == CardKind.Indulgence)).Concat(enemies.Where(player => !player.Judgments.Any(card => DelayedKind(player, card) == CardKind.Indulgence))))
                if (ExecuteBatchThreeSkill(actor, null, target, null)) return true;
        }
        if (!actor.SkillsDisabled && ((actor.SkillIdentity == "re_sunquan" && actor.HasAbility("制衡"))) && !actor.SkillUsed && He(actor).Any(card => KeepValue(card) <= 4)) return ExecuteBatchThreeSkill(actor, null, null, null);
        if (!actor.SkillsDisabled && ((actor.SkillIdentity == "re_lvmeng" && actor.HasAbility("攻心"))) && actor.Awakened && !actor.SkillUsed)
        { Player? target = enemies.FirstOrDefault(player => player.Hand.Count > 0); if (target != null) return ExecuteBatchThreeSkill(actor, null, target, null); }
        Card? wine = actor.Hand.FirstOrDefault(card => card.Kind == CardKind.Wine);
        if (wine != null && !(((actor.SkillIdentity == "re_zhaoyun" && actor.HasAbility("龙胆"))) && !actor.SkillsDisabled && actor.Health < actor.MaxHealth) && actor.SlashCount == 0 && actor.Mark("wineUsed") == 0 && actor.Hand.Any(card => card.Kind == CardKind.Slash) && enemies.Any(player => Distance(actor, player) <= actor.AttackRange))
        { ResolveCard(actor, wine, null, false); return true; }
        Card? chain = actor.Hand.FirstOrDefault(card => card.Kind == CardKind.IronChain);
        if (chain != null && aiRecasted.Add(actor.Seat)) { ResolveCard(actor, chain, null, false); return true; }
        Card? lightning = actor.Hand.FirstOrDefault(card => card.Kind == CardKind.Lightning);
        if (lightning != null && enemies.Count > Players.Count(player => player.IsAlive && player != actor && Allied(actor, player)) && ValidatePlay(actor, lightning, null) == null)
        { ResolveCard(actor, lightning, null, false); return true; }
        return false;
    }
}
