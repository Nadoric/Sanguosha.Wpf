namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private Card? lastResponseCard;

    private void AskJijiang(Player lord, Action<bool> done)
    {
        List<Player> helpers = Enumerable.Range(1, Players.Count - 1).Select(offset => Players[(lord.Seat + offset) % Players.Count])
            .Where(player => player.IsAlive && player.Kingdom == "蜀").ToList();
        void Ask(int index)
        {
            if (index >= helpers.Count) { done(false); return; }
            Player helper = helpers[index];
            OptionalSkill(helper, $"激将：是否为主公{lord.Hero.Name}打出杀？", Allied(helper, lord), use =>
            {
                if (!use) Ask(index + 1);
                else Request(helper, ResponseKind.Slash, "激将：请打出杀，或放弃", success => { if (success) done(true); else Ask(index + 1); }, false);
            });
        }
        EmitSkill(lord, "激将"); Ask(0);
    }

    private bool UseJijiang(Player actor, Player? target)
    {
        if (!actor.HasAbility("激将") || actor.Role != Role.Lord || Mode == GameMode.Team2V2 || actor.JijiangFailed || target == null || target == actor || !target.IsAlive ||
            actor.SlashCount >= 1 && !actor.Equipment.Any(card => card.Kind == CardKind.Crossbow) || Distance(actor, target) > actor.AttackRange ||
            ((target.RuleId == "zhugeliang" && target.HasAbility("空城"))) && target.Hand.Count == 0) return false;
        Phase = GamePhase.Resolving;
        AskJijiang(actor, success =>
        {
            if (!success) { actor.JijiangFailed = true; CompleteAction(actor); return; }
            actor.SlashCount++; actor.PlayedSlash = true;
            Card card = lastResponseCard ?? new Card(0, CardKind.Slash, "", 0);
            usedSlashNatures[card.Id] = card.Kind == CardKind.Slash ? card.Nature : DamageNature.Normal;
            EmitCue(GameCueKind.CardPlayed, actor, CardKind.Slash, target: target, physicalCard: card);
            actor.SetMark("wineSlash", actor.Mark("wine")); actor.SetMark("wine", 0);
            BeginCardUse(actor, CardKind.Slash, card.Id > 0 ? new[] { card } : Array.Empty<Card>(), new[] { target }, finish => DeclaredSlash(actor, target, card, finish), () => { actor.SetMark("wineSlash", 0); CompleteAction(actor); });
        }); return true;
    }
}
