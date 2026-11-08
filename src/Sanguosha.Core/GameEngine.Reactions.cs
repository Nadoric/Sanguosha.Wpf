namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private readonly Queue<Action<Action>> reactions = new();
    private readonly Dictionary<int, int> equipmentLossCounts = new();
    private bool resolvingReaction;
    public List<Card> OrderingCards { get; } = new();

    private bool DrainReactions(Action continuation)
    {
        if (Phase == GamePhase.GameOver) { reactions.Clear(); return false; }
        if (resolvingReaction || !reactions.TryDequeue(out Action<Action>? reaction)) return false;
        resolvingReaction = true;
        reaction(() => { resolvingReaction = false; AfterLosses(continuation); });
        return true;
    }

    private void AfterLosses(Action continuation)
    {
        if (Phase == GamePhase.GameOver) return;
        if (!DrainReactions(continuation)) continuation();
    }

    private void QueueLianying(Player player)
    {
        reactions.Enqueue(done =>
        {
            if (!player.IsAlive || !((player.RuleId == "luxun" && player.HasAbility("连营"))) || player.Hand.Count > 0) { done(); return; }
            OptionalSkill(player, "连营：失去最后一张手牌，是否摸一张？", true, use =>
            { if (use) { Draw(player, 1); EmitSkill(player, "连营"); } done(); });
        });
    }

    private void QueueXiaoji(Player player)
    {
        if (equipmentLossCounts.TryGetValue(player.Seat, out int existing)) { equipmentLossCounts[player.Seat] = existing + 1; return; }
        equipmentLossCounts[player.Seat] = 1;
        reactions.Enqueue(done =>
        {
            int count = equipmentLossCounts.GetValueOrDefault(player.Seat, 1); equipmentLossCounts.Remove(player.Seat);
            if (!player.IsAlive || !((player.RuleId == "sunshangxiang" && player.HasAbility("枭姬")) || (player.RuleId == "re_sunshangxiang" && player.HasAbility("枭姬")))) { done(); return; }
            OptionalSkill(player, $"枭姬：本次失去{count}张装备，是否摸{count * 2}张？", true, use =>
            { if (use) { Draw(player, count * 2); EmitSkill(player, "枭姬"); } done(); });
        });
    }
}
