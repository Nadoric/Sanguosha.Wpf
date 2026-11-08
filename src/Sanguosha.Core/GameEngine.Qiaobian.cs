namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        private void QiaobianCost(Player actor, string phase, bool aiUse, Action<bool> done)
        {
            if (actor.Hand.Count == 0) { done(false); return; }
            List<SkillDecisionOption> options = actor.Hand.Select(card => new SkillDecisionOption(card.Id.ToString(),
                $"弃置{card.Suit}{card.RankText} {card.Name}，跳过{phase}阶段", PreviewCard: card)).ToList();
            options.Add(new("no", "不发动巧变"));
            string ai = aiUse ? actor.Hand.OrderBy(KeepValue).First().Id.ToString() : "no";
            Decide(actor, $"巧变 · {phase}阶段：可弃置一张手牌，跳过该阶段。", options, ai, key =>
            {
                if (key == "no") { done(false); return; }
                Card cost = actor.Hand.Single(card => card.Id.ToString() == key);
                DiscardFromHand(actor, cost);
                EmitSkill(actor, "巧变");
                AddLog($"张郃发动巧变，跳过{phase}阶段。");
                done(true);
            });
        }

        private void ResolveQiaobianStart(Player actor)
        {
            void DrawChoice(bool skipPlay, bool skipDraw)
            {
                if (skipDraw) { QiaobianPlay(actor, skipPlay); return; }
                bool steal = Players.Count(player => player.IsAlive && player != actor && player.Hand.Count > 0 && Hostility(actor, player) > 0) >= 2;
                QiaobianCost(actor, "摸牌", steal, skip =>
                {
                    Action next = () => QiaobianPlay(actor, skipPlay);
                    if (skip) QiaobianSteal(actor, new HashSet<int>(), next); else { Draw(actor, 2); next(); }
                });
            }
            QiaobianCost(actor, "判定", actor.Judgments.Count > 0, skip =>
            { if (skip) DrawChoice(false, false); else ResolveJudgments(actor, false, false, DrawChoice); });
        }

        private void QiaobianSteal(Player actor, HashSet<int> chosen, Action done)
        {
            List<Player> targets = Players.Where(player => player.IsAlive && player != actor &&
                player.Hand.Count > 0 && !chosen.Contains(player.Seat)).ToList();
            if (chosen.Count == 2 || targets.Count == 0) { done(); return; }
            List<SkillDecisionOption> options = targets.Select(player => new SkillDecisionOption(player.Seat.ToString(),
                $"获得{player.Hero.Name}一张手牌（共{player.Hand.Count}张）", PreviewHeroId: player.Hero.Id)).ToList();
            options.Add(new("stop", "结束选取"));
            Player best = targets.OrderByDescending(player => Hostility(actor, player)).First();
            Decide(actor, "巧变：可从至多两名不同角色各获得一张手牌。", options,
                Hostility(actor, best) > 0 ? best.Seat.ToString() : "stop", key =>
                {
                    if (key == "stop") { done(); return; }
                    Player target = targets.Single(player => player.Seat.ToString() == key);
                    chosen.Add(target.Seat);
                    ChooseOtherHandCard(actor, target, () => QiaobianSteal(actor, chosen, done));
                });
        }

        private void QiaobianPlay(Player actor, bool skipPlay)
        {
            if (skipPlay) { BeginDiscard(actor); return; }
            bool usefulMove = Players.Any(player => player.IsAlive && Allied(actor, player) && player.Judgments.Count > 0);
            QiaobianCost(actor, "出牌", usefulMove, skip =>
            {
                if (skip) MoveQiaobianCard(actor, () => BeginDiscard(actor));
                else CompleteAction(actor);
            });
        }

        private void MoveQiaobianCard(Player actor, Action done)
        {
            List<(Player Owner, Card Card, bool Judgment)> movable = new();
            List<Player> Destinations(Player owner, Card card, bool judgment) => Players.Where(player =>
                player.IsAlive && player != owner && (judgment ? player.Judgments.All(item => DelayedKind(player, item) != DelayedKind(owner, card)) :
                    player.Equipment.All(item => EquipmentSlot(item) != EquipmentSlot(card)))).ToList();
            foreach (Player owner in Players.Where(player => player.IsAlive))
            {
                foreach (Card card in owner.Equipment)
                    if (Destinations(owner, card, false).Count > 0) movable.Add((owner, card, false));
                foreach (Card card in owner.Judgments)
                    if (Destinations(owner, card, true).Count > 0) movable.Add((owner, card, true));
            }
            if (movable.Count == 0) { done(); return; }
            List<SkillDecisionOption> options = movable.Select(item => new SkillDecisionOption(item.Card.Id.ToString(),
                $"移动{item.Owner.Hero.Name}的{(item.Judgment ? (new Card(0, DelayedKind(item.Owner, item.Card), "", 0).Name) : item.Card.Name)}（{item.Card.Suit}{item.Card.RankText}）",
                PreviewCard: item.Judgment ? item.Card with { Kind = DelayedKind(item.Owner, item.Card) } : item.Card)).ToList();
            options.Add(new("stop", "不移动"));
            var best = movable.OrderByDescending(item => item.Judgment ? (Allied(actor, item.Owner) ? 10 : -10) : Hostility(actor, item.Owner)).First();
            Decide(actor, "巧变：可移动场上一张装备或判定牌。", options, best.Card.Id.ToString(), key =>
            {
                if (key == "stop") { done(); return; }
                var item = movable.Single(item => item.Card.Id.ToString() == key);
                List<Player> destinations = Destinations(item.Owner, item.Card, item.Judgment);
                Player destination = destinations.OrderByDescending(player => item.Judgment ? Hostility(actor, player) : -Hostility(actor, player)).First();
                Decide(actor, "巧变：请选择合法的目的区域。", destinations.Select(player =>
                    new SkillDecisionOption(player.Seat.ToString(), $"{player.Hero.Name}的{(item.Judgment ? "判定" : "装备")}区",
                        PreviewHeroId: player.Hero.Id)).ToArray(),
                    destination.Seat.ToString(), seat =>
                    {
                        Player target = destinations.Single(player => player.Seat.ToString() == seat);
                        CardKind effective = item.Judgment ? DelayedKind(item.Owner, item.Card) : item.Card.Kind;
                        if (item.Judgment)
                        {
                            bool shortage = item.Owner.SupplyShortages.Remove(item.Card.Id);
                            item.Owner.Judgments.Remove(item.Card);
                            target.Judgments.Add(item.Card);
                            if (shortage) target.SupplyShortages.Add(item.Card.Id);
                        }
                        else
                        {
                            item.Owner.Equipment.Remove(item.Card);
                            target.Equipment.Add(item.Card);
                            OnEquipmentLost(item.Owner);
                        }
                        AddLog($"张郃巧变将{item.Owner.Hero.Name}的{new Card(0, effective, "", 0).Name}移至{target.Hero.Name}区域。");
                        done();
                    });
            });
        }
    }
}
