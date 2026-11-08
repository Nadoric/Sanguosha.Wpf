namespace Sanguosha.Core;
public sealed partial class GameEngine
{
    private bool TryCoreCompletionAi(Player actor)
    {
        if (actor.HasAbility("义绝") && actor.Mark("yijueUsed") == 0 && He(actor).Any() && Players.Any(player => player.IsAlive && player != actor && player.Hand.Count > 0 && Hostility(actor, player) > 0)) return ExecuteYijue(actor);
        if (!CanGuhuo(actor) || actor.Mark("guhuoAiCount") >= 2) return false;
        List<(Card Material, Card Claim, Player Target)> choices = new();
        foreach (Card material in actor.Hand.ToList())
        foreach (Card claim in GuhuoClaims().Where(claim => claim.Kind is CardKind.Slash or CardKind.Peach or CardKind.Duel))
        foreach (Player target in Players.Where(player => player.IsAlive && (claim.Kind == CardKind.Peach ? player == actor && actor.Health < actor.MaxHealth : player != actor && Hostility(actor, player) > 0)))
            if (ValidateGuhuo(actor, material, claim, target) == null) choices.Add((material, claim, target));
        if (choices.Count == 0) return false;
        var best = choices.OrderByDescending(pair => (pair.Material.Kind == pair.Claim.Kind && pair.Material.Nature == pair.Claim.Nature ? 12 : 0) + (pair.Material.Suit == "♥" ? 8 : 0) + (pair.Claim.Kind == CardKind.Peach && actor.Health <= 2 ? 20 : 0) - KeepValue(pair.Material)).First();
        actor.SetMark("guhuoAiCount", actor.Mark("guhuoAiCount") + 1); Phase = GamePhase.Resolving;
        PlayGuhuo(actor, best.Material, best.Claim, new() { best.Target }, () => CompleteAction(actor)); return true;
    }
}
