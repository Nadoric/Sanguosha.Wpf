namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private readonly Dictionary<int, DamageNature> usedSlashNatures = new();
    private void DeclaredSlash(Player actor, Player target, Card card, Action done)
    {
        void Resolve(List<Player> targets, int index)
        {
            if (index >= targets.Count || Phase == GamePhase.GameOver || !actor.IsAlive) { done(); return; }
            if (!targets[index].IsAlive) { Resolve(targets, index + 1); return; }
            ResolveSlashDefense(actor, targets[index], card, () => Resolve(targets, index + 1));
        }
        List<Player> finalTargets = cardUses.LastOrDefault(frame => frame.Actor == actor && frame.Kind == CardKind.Slash)?.Targets.ToList() ?? new() { target };
        Resolve(finalTargets, 0);
    }

    private void ResolveSlashDefense(Player actor, Player target, Card card, Action done)
    {
        void DefendCore(Player finalTarget)
        {
            if (actor.SkillIdentity == "re_machao" && actor.HasAbility("铁骑")) { RefreshedTieji(actor, finalTarget, card, done); return; }
            if (((actor.RuleId == "machao" && actor.HasAbility("铁骑"))))
            {
                OptionalSkill(actor, "铁骑：是否判定？红色令目标不能使用闪", true, use =>
                {
                    if (!use) { ResolveSlashDefenseCore(actor, finalTarget, card, done); return; }
                    EmitSkill(actor, "铁骑"); Judge(actor, "铁骑", judgment =>
                    { if (judgment != null && RedFor(actor, judgment)) Damage(actor, finalTarget, card, done, CardKind.Slash); else ResolveSlashDefenseCore(actor, finalTarget, card, done); });
                });
            }
            else ResolveSlashDefenseCore(actor, finalTarget, card, done);
        }
        void Defend(Player finalTarget) => BeforeSlashTarget(actor, finalTarget, card, DefendCore, done);
        if (!((target.RuleId == "daqiao" && target.HasAbility("流离")) || (target.RuleId == "re_daqiao" && target.HasAbility("流离")))) { Defend(target); return; }
        List<(Card Cost, Player Target)> choices = new();
        foreach (Card cost in He(target))
        {
            int range = target.Equipment.Contains(cost) && EquipmentSlot(cost) == 0 ? 1 : target.AttackRange;
            foreach (Player other in Players.Where(player => player.IsAlive && player != actor && player != target && !(cardUses.LastOrDefault()?.Targets.Contains(player) ?? false) &&
                Distance(target, player) <= range && !(((player.RuleId == "zhugeliang" && player.HasAbility("空城"))) && player.Hand.Count == 0))) choices.Add((cost, other));
        }
        if (choices.Count == 0) { Defend(target); return; }
        List<SkillDecisionOption> options = choices.Select(choice => new SkillDecisionOption($"{choice.Cost.Id}:{choice.Target.Seat}", $"弃{CardLabel(choice.Cost)} → 转给{choice.Target.Hero.Name}", PreviewCard: choice.Cost)).ToList();
        options.Add(new("no", "不发动流离"));
        var best = choices.MaxBy(choice => Hostility(target, choice.Target));
        Decide(target, "流离：弃一牌，将杀转移给攻击范围内除来源外的其他角色", options,
            Hostility(target, best.Target) > 0 ? $"{best.Cost.Id}:{best.Target.Seat}" : "no", key =>
            {
                if (key == "no") { Defend(target); return; }
                var choice = choices.First(choice => $"{choice.Cost.Id}:{choice.Target.Seat}" == key);
                PayCard(target, choice.Cost); EmitSkill(target, "流离"); if (cardUses.LastOrDefault() is { } frame) { frame.Targets.Remove(target); frame.Targets.Add(choice.Target); } Defend(choice.Target);
            });
    }
}
