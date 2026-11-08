namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    // Attribute damage shares the same rescue and post-damage pipeline. Propagated chain
    // damage keeps the original amount/source and cannot start a second propagation wave.
    private void ElementalDamage(Player? source, Player victim, Card? card, int amount, DamageNature nature, Action done)
        => ResolveDamageEvent(source, victim, card, card?.Kind, amount, nature, false, true, done);

    private void ResolveDamageEvent(Player? source, Player victim, Card? card, CardKind? kind, int amount,
        DamageNature nature, bool chain, bool canTransfer, Action done)
    {
        if (!victim.IsAlive || amount <= 0 || Phase == GamePhase.GameOver) { done(); return; }
        List<Card> hearts = victim.Hand.Where(item => SuitFor(victim, item) == "♥").ToList();
        if (canTransfer && ((victim.RuleId == "xiaoqiao" && victim.HasAbility("天香"))) && hearts.Count > 0)
        {
            OptionalSkill(victim, $"天香：弃一红桃手牌，转移整次{amount}点伤害？",
                Players.Any(other => other.IsAlive && other != victim && (Hostility(victim, other) > 0 || other.Health > amount)), use =>
            {
                if (!use) { ResolveDamageEvent(source, victim, card, kind, amount, nature, chain, false, done); return; }
                ChooseOwnCards(victim, "天香：选择红桃手牌（红颜将黑桃视为红桃）", 1, 1, false, selected =>
                    ChoosePlayer(victim, "天香：选择伤害承受者", Players.Where(player => player != victim), false,
                        player => Hostility(victim, player) * 5 - (player.MaxHealth - player.Health), target =>
                        {
                            PayCard(victim, selected[0]); EmitSkill(victim, "天香");
                            ResolveDamageEvent(source, target!, card, kind, amount, nature, chain, false, () =>
                            { if (target!.IsAlive) Draw(target, target.MaxHealth - target.Health); done(); });
                        }), item => SuitFor(victim, item) == "♥");
            }); return;
        }
        bool withinOne = source != null && Distance(source, victim) <= 1;
        List<Player> linked = nature != DamageNature.Normal && victim.Chained && !chain
            ? Players.Where(player => player.IsAlive && player.Chained && player != victim).ToList() : new();
        if (nature != DamageNature.Normal && victim.Chained) victim.Chained = false;
        victim.Health -= amount;
        EmitCue(GameCueKind.Damaged, victim, kind, physicalCard: card, amount: amount, source: source, nature: nature);
        AddLog($"{victim.Hero.Name}受到{source?.Hero.Name ?? "无来源效果"}造成的{amount}点{(nature == DamageNature.Fire ? "火焰" : nature == DamageNature.Thunder ? "雷电" : "")}伤害，剩余{victim.Health}点体力。");
        void Spread(int index)
        {
            if (index >= linked.Count || Phase == GamePhase.GameOver) { done(); return; }
            Player target = linked[index];
            if (!target.IsAlive) { Spread(index + 1); return; }
            work.Enqueue(() => ResolveDamageEvent(source, target, card, kind, amount, nature, true, true, () => Spread(index + 1)));
        }
        Action after = () => AfterExpansionDamage(source, victim, card, kind, amount, withinOne, () => Spread(0));
        if (victim.Health > 0) after();
        else if (!TrySurvival(source, victim, after)) StartRescue(source, victim, after);
    }
}
