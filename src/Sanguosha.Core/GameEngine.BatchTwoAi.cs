namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private readonly HashSet<int> aiRecasted = new();
    private bool TryBatchAiSkill(Player actor)
    {
        List<Player> enemies = Players.Where(player => player.IsAlive && player != actor && Hostility(actor, player) > 0).OrderByDescending(player => Hostility(actor, player)).ToList();
        List<Player> friends = Players.Where(player => player.IsAlive && player != actor && Allied(actor, player)).ToList();
        if (((actor.RuleId == "taishici" && actor.HasAbility("天义"))) && !actor.SkillUsed && actor.Hand.Any(card => card.Rank >= 10))
        { Player? target = enemies.FirstOrDefault(player => player.Hand.Count > 0); if (target != null && ExecuteBatchSkill(actor, null, target, null)) return true; }
        if (((actor.RuleId == "jiangwei" && actor.HasAbility("挑衅"))) && !actor.SkillUsed)
        { Player? target = enemies.FirstOrDefault(player => Distance(player, actor) <= player.AttackRange && He(player).Any()); if (target != null && ExecuteBatchSkill(actor, null, target, null)) return true; }
        if (((actor.RuleId == "zhangzhaozhanghong" && actor.HasAbility("直谏"))))
            foreach (Card card in actor.Hand.Where(card => card.IsEquipment))
            { Player? target = friends.FirstOrDefault(player => player.Equipment.All(item => EquipmentSlot(item) != EquipmentSlot(card))); if (target != null && ExecuteBatchSkill(actor, card, target, null)) return true; }
        if (((actor.RuleId == "lusu" && actor.HasAbility("缔盟"))) && !actor.SkillUsed)
        {
            Player? target = enemies.MaxBy(player => player.Hand.Count), other = friends.MinBy(player => player.Hand.Count);
            if (target != null && other != null && target.Hand.Count > other.Hand.Count && Math.Abs(target.Hand.Count - other.Hand.Count) <= He(actor).Count())
                if (ExecuteBatchSkill(actor, null, target, other)) return true;
        }
        if (((actor.RuleId == "re_sunshangxiang" && actor.HasAbility("结姻"))) && !actor.SkillUsed && He(actor).Any())
        { Player? target = friends.FirstOrDefault(player => !player.IsFemale && (actor.Health < actor.MaxHealth || player.Health < player.MaxHealth)); if (target != null && ExecuteBatchSkill(actor, null, target, null)) return true; }
        if (((actor.RuleId == "pangtong" && actor.HasAbility("连环"))) && !aiRecasted.Contains(actor.Seat))
        { Card? card = actor.Hand.FirstOrDefault(card => card.Suit == "♣" && KeepValue(card) < 5); if (card != null) { aiRecasted.Add(actor.Seat); return ExecuteBatchSkill(actor, card, null, null); } }
        if (((actor.RuleId == "zhouyu" && actor.HasAbility("反间"))) && !actor.SkillUsed && actor.Hand.Count > 0 && enemies.Count > 0) return ExecuteStandardSkill(actor, null, enemies[0], null);
        return false;
    }
}
